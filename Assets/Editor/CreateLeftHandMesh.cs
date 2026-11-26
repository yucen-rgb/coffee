using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public static class CreateLeftHandMesh
{
    [MenuItem("Tools/VR Hands/Create Left Hand Mesh")]
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

        Transform leftRoot = null;

        foreach (Transform bone in renderer.bones)
        {
            Transform current = bone;

            while (current != null)
            {
                if (current.name == "J_Left")
                {
                    leftRoot = current;
                    break;
                }

                current = current.parent;
            }

            if (leftRoot != null)
                break;
        }

        if (leftRoot == null)
        {
            Debug.LogError("找不到 J_Left 骨骼。");
            return;
        }

        if (!sourceMesh.isReadable)
        {
            Debug.LogError("Mesh 不可讀取，請開啟 Read/Write。");
            return;
        }

        Transform[] bones = renderer.bones;
        bool[] leftBones = new bool[bones.Length];

        for (int i = 0; i < bones.Length; i++)
        {
            if (bones[i] != null)
            {
                leftBones[i] =
                    bones[i] == leftRoot ||
                    bones[i].IsChildOf(leftRoot);
            }
        }

        BoneWeight[] weights = sourceMesh.boneWeights;
        List<int>[] leftTriangles =
            new List<int>[sourceMesh.subMeshCount];

        for (int i = 0; i < leftTriangles.Length; i++)
        {
            leftTriangles[i] = new List<int>();
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
                    (GetLeftWeight(weights[a], leftBones) +
                     GetLeftWeight(weights[b], leftBones) +
                     GetLeftWeight(weights[c], leftBones)) / 3f;

                if (score >= 0.5f)
                {
                    leftTriangles[sub].Add(a);
                    leftTriangles[sub].Add(b);
                    leftTriangles[sub].Add(c);
                }
            }
        }

        Mesh newMesh = Object.Instantiate(sourceMesh);
        newMesh.name = "SM_LeftHand_Only";

        for (int i = 0; i < leftTriangles.Length; i++)
        {
            newMesh.SetTriangles(leftTriangles[i], i, false);
        }

        newMesh.RecalculateBounds();

        string path = EditorUtility.SaveFilePanelInProject(
            "Save Left Hand Mesh",
            "SM_LeftHand_Only",
            "asset",
            "請選擇左手 Mesh 的儲存位置"
        );

        if (string.IsNullOrEmpty(path))
        {
            Object.DestroyImmediate(newMesh);
            return;
        }

        AssetDatabase.CreateAsset(newMesh, path);
        AssetDatabase.SaveAssets();

        Undo.RecordObject(renderer, "Assign Left Hand Mesh");
        renderer.sharedMesh = newMesh;

        EditorUtility.SetDirty(renderer);

        Debug.Log("左手 Mesh 建立完成：" + path);
    }

    private static float GetLeftWeight(BoneWeight weight,bool[] leftBones)
    {
        float result = 0f;

        if (weight.boneIndex0 < leftBones.Length &&
            leftBones[weight.boneIndex0])
            result += weight.weight0;

        if (weight.boneIndex1 < leftBones.Length &&
            leftBones[weight.boneIndex1])
            result += weight.weight1;

        if (weight.boneIndex2 < leftBones.Length &&
            leftBones[weight.boneIndex2])
            result += weight.weight2;

        if (weight.boneIndex3 < leftBones.Length &&
            leftBones[weight.boneIndex3])
            result += weight.weight3;

        return result;
    }
}
