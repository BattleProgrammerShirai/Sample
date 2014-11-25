#region ファイル説明
//-----------------------------------------------------------------------------
// SubDivider.cs
//
// Catmull-Clarkアルゴリズムは
// "Real-Time Rendering Third Edition" ISBN-978-1-56881-424-7の
// 13.5.4 Catmull-Clark Subdivision (p.623)を参考に実装
//=============================================================================
#endregion

#region Using ステートメント

using Microsoft.Xna.Framework;

#endregion

namespace MetasequoiaPipeline
{
    /// <summary>
    /// Catmull-Clarkサブディビジョンクラス
    /// </summary>
    public class SubDivider
    {
        /// <summary>
        /// Catmull-Clarkサブディビジョンの適用
        /// </summary>
        /// <returns>分割適用後のメッシュ</returns>
        public MqMesh SubDivide(MqMesh original)
        {
            // 辺情報を生成する
            original.GenerateEdgeInformation();

            // 変換後のメッシュを格納するオブジェクトを生成する
            target = new MqMesh(original.Faces.Count * 4);
            target.Owner = original.Owner;

            // 面の中心位置を計算し、新しいオブジェクト頂点として追加する
            foreach (MqFace face in original.Faces)
            {
                if (face.Vertices.Length == 2)
                    continue;

                face.SubdividedVertex = target.AddPosition(face.GetCenterPosition());
            }

            // 次レベルの頂点位置を計算する
            foreach (MqFace face in original.Faces)
            {
                if (face.Vertices.Length == 2)
                    continue;

                // 面に使われている頂点の次レベルの位置計算
                foreach (MqVertex vtx in face.Vertices)
                    ProcessVertex(vtx);

                // 使用されている辺の次レベルの頂点位置計算
                foreach (MqEdge edge in face.Edges)
                    ProcessEdge(edge);

                // 分割後の面を生成する
                int prevIdx = face.Vertices.Length - 1;
                MqEdge prevEdge = face.GetEdge(prevIdx, 0);
                for (int idx = 0; idx < face.Vertices.Length; ++idx)
                {
                    int nextIdx = (idx < face.Vertices.Length - 1) ? idx + 1 : 0;

                    MqEdge edge = face.GetEdge(idx, nextIdx);
                    MqVertex vtx = face.Vertices[idx];

                    // 新しい頂点から面を生成する
                    MqFace newFace = target.AddFace(
                        prevEdge.SubdividedVertex,
                        vtx.SubdividedVertex,
                        edge.SubdividedVertex,
                        face.SubdividedVertex);

                    // 頂点チャンネル値の計算
                    face.GetInterpolatedEdgeVertexChannelValue(
                        prevIdx, ref newFace.Channels[0]);
                    newFace.Channels[1] = face.Channels[idx];
                    face.GetInterpolatedEdgeVertexChannelValue(
                        idx, ref newFace.Channels[2]);
                    face.GetCenterVertexChannelValue(ref newFace.Channels[3]);

                    // 面情報のコピー
                    newFace.HasTexcoord = face.HasTexcoord;
                    newFace.HasVertexColor = face.HasVertexColor;
                    newFace.Material = face.Material;

                    // 新しい面はボーンウェイトを持つか？
                    newFace.HasBoneWeights = false;
                    for (int i = 0; i < newFace.Channels.Length; ++i)
                    {
                        if (newFace.Channels[i].BoneWeights != null)
                        {
                            newFace.HasBoneWeights = true;
                            break;
                        }
                    }

                    prevEdge = edge;
                    prevIdx = idx;
                }

            }

            return target;
        }

        #region プライベートメソッド

        /// <summary>
        /// 次レベルの頂点位置を計算する
        /// </summary>
        private void ProcessVertex(MqVertex vtx)
        {
            if (vtx.SubdividedVertex != null) return;

            float n = (float)vtx.Faces.Count;
            float factor = 1.0f / (n * n);
            bool isEdgeVertex = false;

            // 頂点に隣接する辺と面の数が違う(両面ポリゴン時に発生)、
            // その割合を重みに当てはめる
            if (vtx.Faces.Count != vtx.Edges.Count)
                factor *= n / (float)vtx.Edges.Count;

            // 隣接する辺の頂点位置を追加する
            Vector3 e = Vector3.Zero;
            foreach (MqEdge edge in vtx.Edges)
            {
                if (edge.Faces.Count == 1)
                {
                    isEdgeVertex = true;
                    break;
                }

                e += edge.GetOtherSide(vtx).Position * factor;
            }

            if (isEdgeVertex)
            {
                vtx.SubdividedVertex = target.AddPosition(vtx.Position);
            }
            else
            {
                // 面の中心位置を追加する
                n = (float)vtx.Faces.Count;
                factor = 1.0f / (n * n);
                Vector3 f = Vector3.Zero;
                foreach (MqFace face in vtx.Faces)
                    f += face.SubdividedVertex.Position * factor;

                // 新しい頂点位置の計算
                Vector3 p = vtx.Position * ((n - 2) / n) + e + f;

                vtx.SubdividedVertex = target.AddPosition(p);
            }
        }

        /// <summary>
        /// 次レベルの辺の頂点位置を計算する
        /// </summary>
        /// <param name="edge"></param>
        private void ProcessEdge(MqEdge edge)
        {
            if (edge.SubdividedVertex != null) return;

            Vector3 p = edge.Vertex0.Position * 0.5f + edge.Vertex1.Position * 0.5f;
            if (edge.Faces.Count >= 2)
            {
                float factor = 1.0f / (edge.Faces.Count + 2);
                Vector3 v = Vector3.Zero;
                foreach (MqFace face in edge.Faces)
                    v += face.SubdividedVertex.Position * factor;

                p = edge.Vertex0.Position * factor +
                    edge.Vertex1.Position * factor +
                    v;
            }

            edge.SubdividedVertex = target.AddPosition(p);
        }

        #endregion

        #region フィールド

        // 次レベルのメッシュ
        MqMesh target;

        #endregion

    }
}
