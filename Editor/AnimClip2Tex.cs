using UnityEngine;
using UnityEditor;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using System.IO;

public class AnimClip2Tex : OdinEditorWindow {

    const string OutPutDir = "Assets/RoleAssets/clip";

    public static bool UseMsg = true;

    [MenuItem("Tools/动画渲染到纹理")]
    private static void OpenWindow()
    {
        GetWindow<AnimClip2Tex>().Show();
        if (!Directory.Exists(OutPutDir))
            Directory.CreateDirectory(OutPutDir);
    }

    private static void Msg(string info)
    {
        if (!UseMsg) return;
        EditorUtility.DisplayDialog("动画渲染到纹理", info, "确定");
    }

    [Title("欲渲染之蒙皮模型")]
    public SkinnedMeshRenderer skinnedRenderer;
    [Title("播放动画之组件")]
    public Animation animation;
    [Title("欲渲染之动画")]
    public AnimationClip clip;
    [Title("欲渲染之尺寸")]
    [Range(64, 4096)]
    public int width;
    [Title("最大空间长度")]
    [Range(0.1f, 10f)]
    public float maxSpaceSize;
    [Title("精度")]
    [Range(1, 3)]
    public int accuracy;
    [Title("信息")]
    [ReadOnly]
    public int vertexCount;
    [ReadOnly]
    public int animFrameCount;
    [ReadOnly]
    public int height;

    public Texture2D tex;

    [Button("采样")]
    private void Build()
    {
        if (!CreateAnimTex(animation, skinnedRenderer, clip, width, vertexCount, animFrameCount, maxSpaceSize, accuracy, out tex))
        {
            Msg("无法渲染，请检查！");
            return;
        }
        var bytes = tex.EncodeToPNG();
        var name = string.Format("{0}/{1}_{2}.png", OutPutDir, animation.gameObject.name, clip.name);
        File.WriteAllBytes(name, bytes);
        Msg(string.Format("已保存到{0}", name));
        AssetDatabase.Refresh();
        // 修改下导入设置
        TextureImporter timporter = TextureImporter.GetAtPath(name) as TextureImporter;
        if (timporter)
        {
            timporter.filterMode = FilterMode.Point;
            timporter.wrapMode = TextureWrapMode.Clamp;
            timporter.mipmapEnabled = false;
            timporter.textureCompression = TextureImporterCompression.Uncompressed;
            timporter.npotScale = TextureImporterNPOTScale.None;
            timporter.sRGBTexture = true;
            timporter.alphaIsTransparency = false;
        }
        AssetDatabase.Refresh();
    }

    protected override void OnGUI()
    {
        base.OnGUI();
        if (clip != null && animation != null && skinnedRenderer != null && skinnedRenderer.sharedMesh != null)
        {
            vertexCount = skinnedRenderer.sharedMesh.vertexCount;
            animFrameCount = (int)(clip.length * clip.frameRate);
            height = Mathf.CeilToInt((float)vertexCount * animFrameCount / width);
        }
    }

    protected override void Initialize()
    {
        base.Initialize();
    }

    public static bool CreateAnimTex(Animation animation, SkinnedMeshRenderer skinnedMeshRenderer, AnimationClip clip,
        int width, int vertexCount, int animFrameCount, float maxSize, int accuracy, out Texture2D animTex)
    {
        if (vertexCount == 0 || animFrameCount == 0)
        {
            animTex = null;
            return false;
        }
        if (animation.GetClip(clip.name) != null)
            animation.RemoveClip(clip.name);
        animation.AddClip(clip, clip.name);
        animation.Play(clip.name);
        // 开始采样
        int lines = Mathf.CeilToInt((float)vertexCount * animFrameCount * accuracy / width);
        Texture2D result = new Texture2D(width, lines, TextureFormat.RGB24, false);
        result.filterMode = FilterMode.Point;
        result.wrapMode = TextureWrapMode.Clamp;
        Color[] colors = new Color[width * lines];
        for (int i = 0; i < animFrameCount; i++)
        {
            float time = (float)i / (animFrameCount - 1);
            animation[clip.name].normalizedTime = time;
            animation.Sample();
            Mesh mesh = new Mesh();
            skinnedMeshRenderer.BakeMesh(mesh);
            var vertices = mesh.vertices;
            for(int j = 0; j < vertexCount; j++)
            {
                Color color = new Color();
                var v = vertices[j];
                color.r = v.x / maxSize * 0.5f + 0.5f;
                color.g = v.y / maxSize * 0.5f + 0.5f;
                color.b = v.z / maxSize * 0.5f + 0.5f;
                if (accuracy == 1)
                    colors[i * vertexCount + j] = color;
                else if (accuracy == 2)
                {
                    Color color1, color2;
                    Split(color, out color1, out color2);
                    colors[(i * vertexCount + j) * 2] = color1;
                    colors[(i * vertexCount + j) * 2 + 1] = color2;
                }
                else
                {
                    Color color1, color2, color3;
                    Split(color, out color1, out color2, out color3);
                    colors[(i * vertexCount + j) * accuracy] = color1;
                    colors[(i * vertexCount + j) * accuracy + 1] = color2;
                    colors[(i * vertexCount + j) * accuracy + 2] = color3;
                }
            }
        }
        result.SetPixels(colors);
        result.Apply();
        animTex = result;
        return true;
    }

    private static void Split(Color s, out Color r1, out Color r2)
    {
        r1 = new Color();
        r2 = new Color();
        for (int i = 0; i < 3; i++)
        {
            float t = s[i];
            r1[i] = Mathf.Floor(t * 256) / 256;
            r2[i] = t * 256 - Mathf.Floor(t * 256);
        }
    }

    private static void Split(Color s, out Color r1, out Color r2, out Color r3)
    {
        r1 = new Color();
        r2 = new Color();
        r3 = new Color();
        for (int i = 0; i < 3; i++)
        {
            float t = s[i];
            r1[i] = Mathf.Floor(t * 256) / 256;
            r2[i] = t * 256 - Mathf.Floor(t * 256);
            r3[i] = t * 65536 - Mathf.Floor(t * 65536);
        }
    }
}
