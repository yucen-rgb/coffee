
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public static class CreateRightHandMesh
{
    [MenuItem("Tools/VR Hands/Create Right Hand Mesh")]
    private static void CreateMesh()
    {
        GameObject selected = Selection.activeGameObject;

        if (selected == null)
        {
            Debug.LogError("請先選取 SM_HandsVR_Male。");
            return;
        }

        SkinnedMeshRenderer renderer =
            selected.GetComponent<SkinnedMeshRenderer>();

        if (renderer == null || renderer.sharedMesh == null)
        {
            Debug.LogError("找不到 Skinned Mesh Renderer 或 Mesh。");
            return;
        }

        Mesh sourceMesh = renderer.sharedMesh;

        Transform rightRoot = null;

        foreach (Transform bone in renderer.bones)
        {
            Transform current = bone;

            while (current != null)
            {
                if (current.name == "J_Right")
                {
                    rightRoot = current;
                    break;
                }

                current = current.parent;
            }

            if (rightRoot != null)
                break;
        }

        if (rightRoot == null)
        {
            Debug.LogError("找不到 J_Right 骨骼。");
            return;
        }

        if (!sourceMesh.isReadable)
        {
            Debug.LogError("Mesh 不可讀取，請在模型 Import Settings 開啟 Read/Write。");
            return;
        }

        Transform[] bones = renderer.bones;
        bool[] rightBones = new bool[bones.Length];

        for (int i = 0; i < bones.Length; i++)
        {
            if (bones[i] != null)
            {
                rightBones[i] =
                    bones[i] == rightRoot ||
                    bones[i].IsChildOf(rightRoot);
            }
        }

        BoneWeight[] weights = sourceMesh.boneWeights;
        List<int>[] rightTriangles =
            new List<int>[sourceMesh.subMeshCount];

        for (int i = 0; i < rightTriangles.Length; i++)
        {
            rightTriangles[i] = new List<int>();
        }

        for (int sub = 0; sub < sourceMesh.subMeshCount; sub++)
        {
            int[] triangles = sourceMesh.GetTriangles(sub);

            for (int i = 0; i < triangles.Length; i += 3)
            {
                int a = triangles[i];
                int b = triangles[i + 1];
                int c = triangles[i + 2];

                float score =
                    (GetRightWeight(weights[a], rightBones) +
                     GetRightWeight(weights[b], rightBones) +
                     GetRightWeight(weights[c], rightBones)) / 3f;

                if (score >= 0.5f)
                {
                    rightTriangles[sub].Add(a);
                    rightTriangles[sub].Add(b);
                    rightTriangles[sub].Add(c);
                }
            }
        }

        Mesh newMesh = Object.Instantiate(sourceMesh);
        newMesh.name = "SM_RightHand_Only";

        for (int i = 0; i < rightTriangles.Length; i++)
        {
            newMesh.SetTriangles(rightTriangles[i], i, false);
        }

        newMesh.RecalculateBounds();

        string path = EditorUtility.SaveFilePanelInProject(
            "Save Right Hand Mesh",
            "SM_RightHand_Only",
            "asset",
            "請選擇右手 Mesh 的儲存位置"
        );

        if (string.IsNullOrEmpty(path))
        {
            Object.DestroyImmediate(newMesh);
            return;
        }

        AssetDatabase.CreateAsset(newMesh, path);
        AssetDatabase.SaveAssets();

        Undo.RecordObject(renderer, "Assign Right Hand Mesh");
        renderer.sharedMesh = newMesh;

        EditorUtility.SetDirty(renderer);

        Debug.Log("右手 Mesh 建立完成：" + path);
    }

    private static float GetRightWeight(BoneWeight weight,bool[] rightBones)
    {
        float result = 0f;

        if (weight.boneIndex0 < rightBones.Length &&
            rightBones[weight.boneIndex0])
            result += weight.weight0;

        if (weight.boneIndex1 < rightBones.Length &&
            rightBones[weight.boneIndex1])
            result += weight.weight1;

        if (weight.boneIndex2 < rightBones.Length &&
            rightBones[weight.boneIndex2])
            result += weight.weight2;

        if (weight.boneIndex3 < rightBones.Length &&
            rightBones[weight.boneIndex3])
            result += weight.weight3;

        return result;
    }
}
