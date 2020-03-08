using UnityEngine;
using UnityEditor;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using System.IO;
using System.Collections.Generic;

public class BoneAnim2Tex : OdinEditorWindow
{
    public enum UVChannel
    {
        UV1,
        UV2,
        UV3,
        UV4,
        UV5,
        UV6,
        UV7,
        UV8,
    }

    const string OutPutDir = "Assets/RoleAssets/BoneClip";

    public static bool UseMsg = true;

    [MenuItem("Tools/骨骼渲染到纹理")]
    private static void OpenWindow()
    {
        GetWindow<BoneAnim2Tex>().Show();
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
    [Title("欲渲染之Mesh")]
    public Mesh mesh;
    [Title("播放动画之组件")]
    public Animation animation;
    [Title("欲渲染之尺寸")]
    [Range(4, 512)]
    public int width;
    [Title("欲渲染之动画")]
    public AnimationClip clip;
    [Title("是否覆盖原先uv")]
    public bool overwrite;
    [Title("将骨骼索引写入到")]
    public UVChannel indexChannel;
    [Title("将骨骼权重写入到")]
    public UVChannel weightChannel;
    [Title("预览")]
    public Texture2D animTex;


    [Title("信息")]
    [ReadOnly]
    public int vertexCount;
    [ReadOnly]
    public int animFrameCount;
    [ReadOnly]
    public int boneCount;
    [ReadOnly]
    public int height;

    protected override void OnGUI()
    {
        base.OnGUI();
        if (mesh != null && clip != null)
        {
            boneCount = mesh.bindposes.Length;
            vertexCount = mesh.vertexCount;
            animFrameCount = (int)(clip.length * clip.frameRate);
            height = Mathf.CeilToInt(boneCount * animFrameCount * 12 / width);
        }
    }

    [Button("烘焙")]
    private void ModifyMesh()
    {
        if (mesh == null || skinnedRenderer == null) return;
        if (vertexCount == 0 || animFrameCount == 0)
        {
            Msg("网格及动画读取失败！");
            return;
        }
        if (mesh.boneWeights == null || mesh.boneWeights.Length == 0)
        {
            Msg("网格中没有骨骼权重信息！");
            return;
        }
        if (boneCount > 255)
        {
            Msg("骨骼数超过255！");
            return;
        }
        if (weightChannel == indexChannel)
        {
            Msg("通道冲突！");
            return;
        }
        if (!MappingBoneWeightToMeshUV(mesh, weightChannel, indexChannel, overwrite))
        {
            Msg("映射失败！所选通道已存在信息！");
            return;
        }
        if (!CreateAnimTex(animation, skinnedRenderer, clip, mesh, width, animFrameCount, out animTex))
        {
            Msg("骨骼与绑定姿势不匹配！");
            return;
        }
        var bytes = animTex.EncodeToPNG();
        var name = string.Format("{0}/{1}_{2}_bone.png", OutPutDir, animation.gameObject.name, clip.name);
        File.WriteAllBytes(name, bytes);
        Msg(string.Format("已保存到{0}", name));
        AssetDatabase.Refresh();
        // 修改下导入设置
        TextureImporter timporter = TextureImporter.GetAtPath(name) as TextureImporter;
        if (timporter)
        {
            TextureImporterSettings tis = new TextureImporterSettings();
            timporter.ReadTextureSettings(tis);
            tis.filterMode = FilterMode.Point;
            tis.npotScale = TextureImporterNPOTScale.None;
            tis.mipmapEnabled = false;
            timporter.SetTextureSettings(tis);
            AssetDatabase.ImportAsset(name);
        }
    }

    private static Vector4 EncodeFloatRGBA(float v)
    {
        v = v * 0.01f + 0.5f;
        Vector4 kEncodeMul = new Vector4(1.0f, 255.0f, 65025.0f, 160581375.0f);
        float kEncodeBit = 1.0f / 255.0f;
        Vector4 enc = kEncodeMul * v;
        for (int i = 0; i < 4; i++)
            enc[i] = enc[i] - Mathf.Floor(enc[i]);
        enc = enc - new Vector4(enc.y, enc.z, enc.w, enc.w) * kEncodeBit;
        return enc;
    }

    private static bool CreateAnimTex(Animation animation, SkinnedMeshRenderer skinnedMeshRenderer, AnimationClip clip, Mesh mesh,
        int width, int animFrameCount, out Texture2D animTex)
    {
        animTex = null;
        Matrix4x4[] bindPoses = mesh.bindposes;
        Transform[] bones = skinnedMeshRenderer.bones;
        int bonesCount = bones.Length;
        if (bindPoses.Length != bones.Length)
            return false;
        if (animation.GetClip(clip.name) != null)
            animation.RemoveClip(clip.name);
        animation.AddClip(clip, clip.name);
        animation.Play(clip.name);
        // 开始采样
        int lines = Mathf.CeilToInt((float)bones.Length * animFrameCount * 12 / width);
        Texture2D result = new Texture2D(width, lines, TextureFormat.RGBA32, false);
        result.filterMode = FilterMode.Point;
        result.wrapMode = TextureWrapMode.Clamp;
        Color[] colors = new Color[width * lines * 3];
        // 逐帧写入矩阵
        for (int i = 0; i < animFrameCount; i++)
        {
            float time = (float)i / (animFrameCount - 1);
            animation[clip.name].normalizedTime = time;
            animation.Sample();
            // 写入变换后的矩阵
            for (int j = 0; j < bonesCount; j++) 
            {
                Matrix4x4 matrix = skinnedMeshRenderer.transform.worldToLocalMatrix * bones[j].localToWorldMatrix * bindPoses[j];
                colors[(i * bonesCount + j) * 12 + 0] = EncodeFloatRGBA(matrix.m00);
                colors[(i * bonesCount + j) * 12 + 1] = EncodeFloatRGBA(matrix.m01);
                colors[(i * bonesCount + j) * 12 + 2] = EncodeFloatRGBA(matrix.m02);
                colors[(i * bonesCount + j) * 12 + 3] = EncodeFloatRGBA(matrix.m03);
                colors[(i * bonesCount + j) * 12 + 4] = EncodeFloatRGBA(matrix.m10);
                colors[(i * bonesCount + j) * 12 + 5] = EncodeFloatRGBA(matrix.m11);
                colors[(i * bonesCount + j) * 12 + 6] = EncodeFloatRGBA(matrix.m12);
                colors[(i * bonesCount + j) * 12 + 7] = EncodeFloatRGBA(matrix.m13);
                colors[(i * bonesCount + j) * 12 + 8] = EncodeFloatRGBA(matrix.m20);
                colors[(i * bonesCount + j) * 12 + 9] = EncodeFloatRGBA(matrix.m21);
                colors[(i * bonesCount + j) * 12 + 10] = EncodeFloatRGBA(matrix.m22);
                colors[(i * bonesCount + j) * 12 + 11] = EncodeFloatRGBA(matrix.m23);
            }
        }
        result.SetPixels(colors);
        result.Apply();
        animTex = result;
        return true;
    }

    private static bool MappingBoneWeightToMeshUV(Mesh mesh, UVChannel weightChannel, UVChannel indexChannel, bool overwrite)
    {
        var boneWeights = mesh.boneWeights;
        List<Vector2> wUV = new List<Vector2>(), iUV = new List<Vector2>();
        mesh.GetUVs((int)weightChannel, wUV);
        mesh.GetUVs((int)indexChannel, iUV);
        if (((wUV != null && wUV.Count != 0) || (iUV != null && iUV.Count != 0)) && !overwrite)
            return false;
        wUV = new List<Vector2>();
        iUV = new List<Vector2>();
        for (int i = 0; i < boneWeights.Length; i++)
        {
            var bw = boneWeights[i];
            iUV.Add(new Vector2(bw.boneIndex0,
                                bw.boneIndex1));
            wUV.Add(new Vector2(bw.weight0, bw.weight1));

        }
        mesh.SetUVs((int)weightChannel, wUV);
        mesh.SetUVs((int)indexChannel, iUV);
        return true;
    }

}
