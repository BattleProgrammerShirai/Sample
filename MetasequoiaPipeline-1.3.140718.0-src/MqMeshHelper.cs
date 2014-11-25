#region ファイル説明
//-----------------------------------------------------------------------------
// MqMeshHelper.cs
//=============================================================================
#endregion

#region Using ステートメント

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

#endregion

namespace MetasequoiaPipeline
{
    /// <summary>
    /// メッシュ生成ヘルパークラス、ミラーリングと回転体処理をする
    /// </summary>
    static class MqMeshHelper
    {
        #region ミラーリング処理

        /// <summary>
        /// ミラーリングの適用
        /// </summary>
        public static void ApplyMirroring(MqMesh mesh, Matrix nodeTransform)
        {
            MqObject mqObj = mesh.Owner;
            if (mqObj.MirrorSettings == null ||
                mqObj.MirrorSettings.Type == MqMirrorType.None) return;

            if ((mqObj.MirrorSettings.Axis & MqMirrorAxies.X) == MqMirrorAxies.X)
                Mirroring(mesh, new Vector3(-1, 1, 1), nodeTransform);
            if ((mqObj.MirrorSettings.Axis & MqMirrorAxies.Y) == MqMirrorAxies.Y)
                Mirroring(mesh, new Vector3(1, -1, 1), nodeTransform);
            if ((mqObj.MirrorSettings.Axis & MqMirrorAxies.Z) == MqMirrorAxies.Z)
                Mirroring(mesh, new Vector3(1, 1, -1), nodeTransform);
        }

        /// <summary>
        /// 指定軸のミラーリング処理
        /// </summary>
        static void Mirroring(MqMesh mesh, Vector3 mirror, Matrix nodeTransform)
        {
            Matrix xform = Matrix.CreateScale(mirror);

            if ((mesh.Owner.MirrorSettings.Axis & MqMirrorAxies.Local) != MqMirrorAxies.Local)
                xform = nodeTransform * xform * Matrix.Invert(nodeTransform);

            int numFaces = mesh.Faces.Count;
            int numVertices = mesh.Vertices.Count;
            MqObject mqObj = mesh.Owner;
            MqMirrorType mirrorType = mqObj.MirrorSettings.Type;

            // 接続ミラーリングの場合には辺情報を生成する
            if (mirrorType == MqMirrorType.Connect)
                mesh.GenerateEdgeInformation();

            float epsilon = 1e-3f;

            // 頂点のミラーリング
            int[] newVertices = new int[numVertices];
            for (int i = 0; i < numVertices; ++i)
            {
                Vector3 orgPos = mesh.Vertices[i].Position;
                Vector3 pos = Vector3.Transform(orgPos, xform);

                // ミラーリング前後の頂点位置が近いか？
                if ((pos - orgPos).Length() < epsilon)
                {
                    // 同じ頂点を使用する
                    newVertices[i] = mesh.Vertices[i].Index;
                }
                else
                {
                    // 新しい頂点を追加する
                    newVertices[i] = mesh.AddPosition(pos).Index;
                }
            }

            // 面のミラーリング。
            int[] vertices = new int[4];
            int[] mapOrgToNewIndices = new int[4];
            int[] mapNewToOrgIndices = new int[4];

            for (int faceIdx = 0; faceIdx < numFaces; ++faceIdx)
            {
                MqFace orgFace = mesh.Faces[faceIdx];

                if (orgFace.Vertices.Length == 2)
                    continue;

                int numFaceVertices = orgFace.Vertices.Length;

                // ミラー処理後は面の向きが逆になるので、頂点リストの順番を反転する
                int newIdx = orgFace.Vertices.Length - 1;
                for (int orgIdx = 0; orgIdx < numFaceVertices; ++orgIdx, --newIdx)
                {
                    vertices[newIdx] = newVertices[orgFace.Vertices[orgIdx].Index];
                    mapNewToOrgIndices[newIdx] = orgIdx;
                    mapOrgToNewIndices[orgIdx] = newIdx;
                }

                // 新しい面の追加
                MqFace newFace = mesh.AddFace(vertices, numFaceVertices);
                CopyFaceInfo(orgFace, newFace);

                // 頂点チャンネル値のコピー
                for (int i = 0; i < numFaceVertices; ++i)
                    newFace.Channels[i] = orgFace.Channels[mapNewToOrgIndices[i]];

                // 開いた辺同士を接続する
                if (mirrorType == MqMirrorType.Connect)
                {
                    foreach (MqEdge edge in orgFace.Edges)
                    {
                        if (edge.Faces.Count != 1) continue;

                        int idx0, idx1;
                        GetEdgeIndices(orgFace, edge, out idx0, out idx1);

                        // 距離制限があるか？
                        if (mqObj.MirrorSettings.Distance.HasValue)
                        {
                            float limitDistance = mqObj.MirrorSettings.Distance.Value * 2;

                            Vector3 p0 = orgFace.Vertices[idx0].Position;
                            Vector3 p1 =
                                newFace.Vertices[mapOrgToNewIndices[idx0]].Position;

                            if ((p1 - p0).Length() >= limitDistance) continue;

                            p0 = orgFace.Vertices[idx1].Position;
                            p1 = newFace.Vertices[mapOrgToNewIndices[idx1]].Position;

                            // 距離制限以内か？
                            if ((p1 - p0).Length() >= limitDistance) continue;
                        }

                        // 接続面を追加する
                        MqFace conectionFace = mesh.AddFace(
                            orgFace.Vertices[idx0].Index,
                            orgFace.Vertices[idx1].Index,
                            newFace.Vertices[mapOrgToNewIndices[idx1]].Index,
                            newFace.Vertices[mapOrgToNewIndices[idx0]].Index
                            );

                        CopyFaceInfo(orgFace, conectionFace);

                        conectionFace.Channels[0] = orgFace.Channels[idx0];
                        conectionFace.Channels[1] = orgFace.Channels[idx1];
                        conectionFace.Channels[2] = orgFace.Channels[idx1];
                        conectionFace.Channels[3] = orgFace.Channels[idx0];
                    }
                }

            }

        }

        /// <summary>
        /// 指定した辺のローカルインデックス値を取得する
        /// </summary>
        static void GetEdgeIndices(MqFace face, MqEdge edge, out int idx0, out int idx1)
        {
            int prevIdx = face.Vertices.Length - 1;
            for (int idx = 0; idx < face.Vertices.Length; ++idx)
            {
                if ((face.Vertices[idx] == edge.Vertex0 &&
                    face.Vertices[prevIdx] == edge.Vertex1) ||
                    (face.Vertices[idx] == edge.Vertex1 &&
                    face.Vertices[prevIdx] == edge.Vertex0))
                {
                    idx0 = idx;
                    idx1 = prevIdx;
                    return;
                }

                prevIdx = idx;
            }

            throw new InvalidOperationException(Resources.NotFoundEdge);
        }

        /// <summary>
        /// 面情報のコピー
        /// </summary>
        static void CopyFaceInfo(MqFace orgFace, MqFace newFace)
        {
            newFace.Material = orgFace.Material;
            newFace.HasTexcoord = orgFace.HasTexcoord;
            newFace.HasVertexColor = orgFace.HasVertexColor;
            newFace.HasBoneWeights = orgFace.HasBoneWeights;
        }

        #endregion

        #region 回転体処理

        /// <summary>
        /// 回転体処理を適用する
        /// </summary>
        public static void ApplyLathe(MqMesh mesh, Matrix nodeTransform)
        {
            if (mesh.Owner.LatheSettings == null ||
                mesh.Owner.LatheSettings.Type == MqLatheType.None) return;

            int latheSegments = mesh.Owner.LatheSettings.NumSegments;
            Dictionary<int, int> alreadySeenVertices = new Dictionary<int, int>();

            // 回転行列を計算する
            float step = MathHelper.TwoPi / (float)latheSegments;

            Vector3 axis = Vector3.Zero;
            Func<float, Vector3> rotate = null;

            switch (mesh.Owner.LatheSettings.Axis)
            {
                case MqLatheAxis.X:
                    axis = Vector3.UnitX;
                    rotate = a => new Vector3(0, (float)Math.Cos(a), (float)Math.Sin(a));
                    break;
                case MqLatheAxis.Y:
                    axis = Vector3.UnitY;
                    rotate = a => new Vector3((float)Math.Cos(a), 0, (float)Math.Sin(a));
                    break;
                case MqLatheAxis.Z:
                    axis = Vector3.UnitZ;
                    rotate = a => new Vector3((float)Math.Cos(a), (float)Math.Sin(a), 0);
                    break;
            }

            // 回転した頂点を生成する
            Matrix xform = Matrix.Invert(nodeTransform);
            foreach (MqFace face in mesh.LatheFaces)
            {
                foreach (MqVertex vtx in face.Vertices)
                {
                    if (!alreadySeenVertices.ContainsKey(vtx.Index))
                    {
                        alreadySeenVertices.Add(vtx.Index, mesh.Vertices.Count);

                        Vector3 pos = Vector3.Transform(vtx.Position, nodeTransform);
                        Vector3 center = pos * axis;
                        float radius = (pos - center).Length();

                        float angle = 0;
                        for (int i = 0; i < latheSegments; ++i, angle += step)
                        {
                            pos = center + rotate(angle) * radius;
                            Vector3.Transform(ref pos, ref xform, out pos);
                            mesh.AddPosition(pos);
                        }
                    }
                }
            }

            // 面の生成
            foreach (MqFace face in mesh.LatheFaces)
            {
                int baseIdx0 = alreadySeenVertices[face.Vertices[0].Index];
                int baseIdx1 = alreadySeenVertices[face.Vertices[1].Index];
                int idx0 = baseIdx0 + latheSegments - 1;
                int idx1 = baseIdx1 + latheSegments - 1;

                for (int i = 0; i < latheSegments; ++i)
                {
                    int idx2 = baseIdx0 + i, idx3 = baseIdx1 + i;

                    MqFace newFace = mesh.AddFace(idx0, idx2, idx3, idx1);
                    CopyFaceInfo(face, newFace);

                    newFace = mesh.AddFace(idx0, idx1, idx3, idx2);
                    CopyFaceInfo(face, newFace);

                    idx0 = idx2; idx1 = idx3;
                }
            }

        }

        #endregion

    }
}
