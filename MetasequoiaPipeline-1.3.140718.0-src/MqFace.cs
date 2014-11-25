#region ファイル説明
//-----------------------------------------------------------------------------
// MqFace.cs
//=============================================================================
#endregion

#region Using ステートメント

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content.Pipeline.Graphics;

#endregion

namespace MetasequoiaPipeline
{
    /// <summary>
    /// メタセコイアの面情報を格納するクラス
    /// <summary>
    public sealed class MqFace
    {
        #region プロパティ

        /// <summary>
        /// 面を構成する頂点リストの取得
        /// </summary>
        public MqVertex[] Vertices { get; private set; }

        /// <summary>
        /// 面を構成する頂点チャンネルリストの取得
        /// </summary>
        public MqVertexChannel[] Channels { get; private set; }

        /// <summary>
        /// 面を構成する辺リストの取得
        /// Catmull-Clarkやミラーリング処理時に使用される
        /// </summary>
        public MqEdge[] Edges { get; private set; }

        /// <summary>
        /// 面に使われているマテリアルの取得と設定
        /// </summary>
        public MaterialContent Material { get; set; }

        /// <summary>
        /// この面はテクスチャ座標を使用しているか？
        /// </summary>
        public bool HasTexcoord { get; set; }

        /// <summary>
        /// この面は頂点カラーを使用しているか？
        /// </summary>
        public bool HasVertexColor { get; set; }

        /// この面は頂点ウェイトを使用しているか？
        /// </summary>
        public bool HasBoneWeights { get; set; }

        /// <summary>
        /// 次サブディビジョンレベルの頂点、Catmull-Clark処理時に使用する
        /// </summary>
        public MqVertex SubdividedVertex { get; set; }

        /// <summary>
        /// 面法線, MqMeshBuilderで使用される
        /// </summary>
        public Vector3 Normal { get; set; }

        #endregion

        #region インターナルフィールド

        // マテリアルインデックス、デシリアライズ時に格納される
        internal int materialIdx;

        #endregion

        #region 生成

        /// <summary>
        /// 面の生成、頂点情報との整合性を保つ仕組みはMqMeshクラス内にあるので
        /// このコンストラクタはInternalとなっている
        /// </summary>
        internal MqFace(int numVertices)
        {
            Vertices = new MqVertex[numVertices];
            Channels = new MqVertexChannel[numVertices];
        }

        internal void AllocateEdges()
        {
            Edges = new MqEdge[Vertices.Length];
        }

        #endregion

        #region ヘルパーメソッド

        /// <summary>
        /// 面の中心位置を取得する
        /// </summary>
        public Vector3 GetCenterPosition()
        {
            Vector3 p = Vector3.Zero;
            foreach (MqVertex v in Vertices)
                p += v.Position;

            return p / Vertices.Length;
        }

        /// <summary>
        /// 指定されたローカルインデックスで構成された辺情報を取得する
        /// </summary>
        public MqEdge GetEdge(int idx0, int idx1)
        {
            MqVertex va = Vertices[idx0];
            MqVertex vb = Vertices[idx1];

            foreach (MqEdge edge in Edges)
            {
                if ((edge.Vertex0 == va && edge.Vertex1 == vb) ||
                    (edge.Vertex1 == va && edge.Vertex0 == vb))
                    return edge;
            }

            throw new InvalidOperationException(Resources.MqFaceEdgeNotFounded);
        }

        /// <summary>
        /// 面の中心位置の頂点チャンネル値を計算する。
        /// </summary>
        /// <param name="channel">補間された頂点チャンネル値</param>
        public void GetCenterVertexChannelValue(ref MqVertexChannel channel)
        {
            // テクスチャ座標と頂点カラーの補間
            Vector2 texCoord = Vector2.Zero;
            Vector4 color = Vector4.Zero;

            float factor = 1.0f / (float)Channels.Length;
            bool hasWeights = false;
            for (int i = 0; i < Channels.Length; ++i)
            {
                texCoord += Channels[i].Texcoord * factor;
                color += Channels[i].Color.ToVector4() * factor;
                if (Channels[i].BoneWeights != null)
                    hasWeights = true;
            }

            channel.Texcoord = texCoord;
            channel.Color = new Color(color);

            // ボーンウェイトの補間
            if (hasWeights)
            {
                BoneWeightCollection weights = new BoneWeightCollection();

                for (int i = 0; i < Channels.Length; ++i)
                {
                    if (Channels[i].BoneWeights != null)
                    {
                        foreach (BoneWeight boneWeight in Channels[i].BoneWeights)
                            AddBoneWeight(weights, boneWeight, factor);
                    }
                }

                channel.BoneWeights = weights;
            }

        }

        /// <summary>
        /// 指定された辺の中心点のチャンネル値を計算する。
        /// </summary>
        /// <param name="channel"></param>
        public void GetInterpolatedEdgeVertexChannelValue(int edgeIdx,
                                                            ref MqVertexChannel channel)
        {
            int nextIdx = (edgeIdx < Channels.Length - 1) ? edgeIdx + 1 : 0;

            // テクスチャ座標の補間
            channel.Texcoord = Vector2.Lerp(
                Channels[edgeIdx].Texcoord,
                Channels[nextIdx].Texcoord, 0.5f);

            // 頂点カラーの補間
            channel.Color = Color.Lerp(
                Channels[edgeIdx].Color,
                Channels[nextIdx].Color, 0.5f);

            // ボーンウェイトの補間
            if (Channels[edgeIdx].BoneWeights != null ||
                Channels[nextIdx].BoneWeights != null)
            {
                BoneWeightCollection weights = new BoneWeightCollection();

                if (Channels[edgeIdx].BoneWeights != null)
                {
                    foreach (BoneWeight boneWeight in Channels[edgeIdx].BoneWeights)
                        AddBoneWeight(weights, boneWeight, 0.5f);
                }

                if (Channels[nextIdx].BoneWeights != null)
                {
                    foreach (BoneWeight boneWeight in Channels[nextIdx].BoneWeights)
                        AddBoneWeight(weights, boneWeight, 0.5f);
                }

                channel.BoneWeights = weights;
            }

        }

        /// <summary>
        /// ボーンウェイトを指定のコレクションへ追加する
        /// </summary>
        void AddBoneWeight(BoneWeightCollection weights,
                            BoneWeight boneWeight, float scale)
        {
            for (int i = 0; i < weights.Count; ++i)
            {
                if (weights[i].BoneName == boneWeight.BoneName)
                {
                    weights[i] = new BoneWeight(boneWeight.BoneName,
                        weights[i].Weight + boneWeight.Weight * scale);
                    return;
                }
            }

            weights.Add(new BoneWeight(boneWeight.BoneName, boneWeight.Weight * scale));
        }

        #endregion
    }
}
