#region ファイル説明
//-----------------------------------------------------------------------------
// MqMesh.cs
//=============================================================================
#endregion

#region Using ステートメント

using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Content.Pipeline.Graphics;

#endregion

namespace MetasequoiaPipeline
{
    /// <summary>
    /// メタセコイアのメッシュ情報(面情報の集合体)を格納するクラス
    /// <summary>
    public class MqMesh
    {
        #region プロパティ

        /// <summary>
        /// このメッシュを所有しているオブジェクトの取得
        /// </summary>
        public MqObject Owner { get; internal set; }

        /// <summary>
        /// このメッシュに使われている頂点リストの取得
        /// </summary>
        public IList<MqVertex> Vertices { get { return _vertices; } }

        [ContentSerializer(ElementName = "Vertices")]
        List<MqVertex> _vertices;

        /// <summary>
        /// 面リストの取得
        /// </summary>
        public IList<MqFace> Faces { get { return _faces; } }

        [ContentSerializer(ElementName = "Faces")]
        List<MqFace> _faces;

        /// <summary>
        /// 回転体用の辺リストの取得
        /// </summary>
        public IList<MqFace> LatheFaces { get { return _latheFaces; } }

        [ContentSerializer(ElementName = "LatheFaces")]
        List<MqFace> _latheFaces;

        #endregion

        #region 生成メソッド

        /// <summary>
        /// メッシュ生成
        /// </summary>
        /// <param name="owner"></param>
        public MqMesh()
        {
            _vertices = new List<MqVertex>();
            _faces = new List<MqFace>();
        }

        /// <summary>
        /// メッシュ生成
        /// </summary>
        /// <param name="owner"></param>
        public MqMesh(int vertexCapacity)
        {
            _vertices = new List<MqVertex>(vertexCapacity);
            _faces = new List<MqFace>();
        }

        #endregion

        #region メッシュ操作メソッド

        /// <summary>
        /// 指定された座標の頂点を追加する
        /// </summary>
        /// <param name="position">頂点位置</param>
        /// <returns>追加された頂点情報</returns>
        public MqVertex AddPosition(Vector3 position)
        {
            MqVertex vtx = new MqVertex(Vertices.Count, position);
            _vertices.Add(vtx);

            return vtx;
        }

        /// <summary>
        /// 指定された頂点インデックスリストを使用した面を追加する
        /// </summary>
        /// <param name="indices">頂点インデックスリスト</param>
        /// <returns>生成された面情報</returns>
        public MqFace AddFace(params int[] indices)
        {
            return AddFace(indices, indices.Length);
        }

        /// <summary>
        /// 指定された頂点インデックスリストを使用した面を追加する
        /// </summary>
        /// <param name="indices">頂点インデックスリスト</param>
        /// <param name="numIndices">インデックス数</param>
        /// <returns>生成された面情報</returns>
        /// <summary>
        public MqFace AddFace(int[] indices, int numIndices)
        {
            MqFace face = new MqFace(numIndices);
            AddFace(face);

            for (int i = 0; i < numIndices; ++i)
            {
                MqVertex vtx = _vertices[indices[i]];
                face.Vertices[i] = vtx;

                vtx.Faces.Add(face);
            }

            return face;
        }

        /// <summary>
        /// 指定された頂点リストを使用した面を追加する
        /// </summary>
        /// <param name="indices">頂点リスト</param>
        /// <returns>生成された面情報</returns>
        /// <summary>
        public MqFace AddFace(params MqVertex[] vertices)
        {
            MqFace face = new MqFace(vertices.Length);
            AddFace(face);

            for (int i = 0; i < vertices.Length; ++i)
            {
                MqVertex vtx = vertices[i];
                face.Vertices[i] = vtx;

                vtx.Faces.Add(face);
            }

            return face;
        }

        /// <summary>
        /// 辺情報を生成する
        /// </summary>
        /// <remarks>
        /// 面タイプがCatmull-Clarkや鏡面接続、回転体処理をする場合に辺情報が必要だが、
        /// 通常のポリゴン面の場合には必要ないので、メモリと処理時間節約の為に
        /// 普段は生成せずにこのメソッドを使って明示的に生成するようになっている
        /// </remarks>
        public void GenerateEdgeInformation()
        {
            edges = new Dictionary<long, MqEdge>();

            foreach (MqVertex vtx in _vertices)
                vtx.AllocateEdges();

            foreach (MqFace face in Faces)
            {
                int numVerts = face.Vertices.Length;
                face.AllocateEdges();
                for (int idx = 0; idx < numVerts; ++idx)
                {
                    int nextIdx = (idx < numVerts - 1) ? idx + 1 : 0;
                    MqVertex vtx = face.Vertices[idx];
                    MqVertex nextVtx = face.Vertices[nextIdx];

                    MqEdge edge = AddEdge(vtx, nextVtx);
                    face.Edges[idx] = edge;

                    edge.Faces.Add(face);
                }
            }
        }

        #endregion

        #region プライベートメンバ

        /// <summary>
        /// 面を追加する
        /// </summary>
        public void AddFace(MqFace face)
        {
            _faces.Add(face);

            if (face.Vertices.Length == 2)
            {
                if (LatheFaces == null) _latheFaces = new List<MqFace>();
                _latheFaces.Add(face);
            }
        }

        /// <summary>
        /// 辺の追加
        /// </summary>
        private MqEdge AddEdge(MqVertex v0, MqVertex v1)
        {
            long id = GetId(v0, v1);
            MqEdge edge;
            if (edges.TryGetValue(id, out edge)) return edge;

            id = GetId(v1, v0);
            if (edges.TryGetValue(id, out edge)) return edge;

            edge = new MqEdge(v0, v1);
            v0.Edges.Add(edge);
            v1.Edges.Add(edge);

            edges.Add(id, edge);

            return edge;
        }

        /// <summary>
        /// 指定された頂点から辺IDを生成する
        /// </summary>
        private long GetId(MqVertex v0, MqVertex v1)
        {
            return (long)((long)v0.Index << 32) + v1.Index;
        }

        /// <summary>
        /// 辺Id(２つの頂点インデックスからなる)から辺情報への辞書
        /// </summary>
        Dictionary<long, MqEdge> edges;

        #endregion

    }

}
