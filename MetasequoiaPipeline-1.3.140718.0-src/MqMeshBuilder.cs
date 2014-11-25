#region ファイル説明
//-----------------------------------------------------------------------------
// MqMeshBuilder.cs
//=============================================================================
#endregion

#region Using ステートメント

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content.Pipeline.Graphics;

#endregion

namespace MetasequoiaPipeline
{
    /// <summary>
    /// メタセコイア向けのメッシュビルダークラス
    /// </summary>
    /// <remarks>
    /// メタセコイアのモデルデータは面毎に異なるマテリアル、
    /// 頂点チャンネル(頂点色、テクスチャ座標)の組み合わせを混合して記述できる。
    /// XNA付属のMeshBuilderはこの異なるマテリアルには対応しているが、
    /// 異なる頂点チャンネルには対応していない。
    /// そこで、このクラスでは異なった頂点チャンネルの面の追加に対応させている。
    /// 但し、実装簡略化の為にMeshBuilderの様に自由なチャンネルを追加することはできない
    /// (メタセコイアで使われているチャンネルにしか対応していない)。
    /// </remarks>
    class MqMeshBuilder
    {
        #region プロパティ

        /// <summary>
        /// インデックスバッファのサイズを16ビットに抑えるか？
        /// </summary>
        public bool UseSixteenBitsIndex { get; set; }

        #endregion

        /// <summary>
        /// メッシュ生成の開始
        /// </summary>
        /// <param name="targetMesh">ジオメトリを追加するMeshContent</param>
        public void BeginBuild(MeshContent targetMesh)
        {
            if (buildingMesh != null)
                throw new InvalidOperationException(Resources.MqMeshBuilderBeginTwice);

            buildingMesh = targetMesh;

            mapToGeometries =
                new Dictionary<MaterialAndVertexChannelKey, GeometryContent>();
        }

        /// <summary>
        /// メッシュ生成の終了
        /// </summary>
        /// <returns>生成したMeshContent</returns>
        public MeshContent EndBuild()
        {
            // 頂点データの最適化、重複している頂点データをマージさせる
            MeshHelper.MergeDuplicateVertices(buildingMesh);

            // 作業用変数をクリアする
            MeshContent result = buildingMesh;
            buildingMesh = null;
            mapToGeometries = null;

            return result;
        }

        /// <summary>
        /// メタセコイアメッシュの追加
        /// </summary>
        /// <param name="mqMesh"></param>
        public void AddMesh(MqMesh mqMesh)
        {
            // 頂点座標の追加
            vertexToFaces = new List<List<MqFace>>(mqMesh.Vertices.Count);
            positionIndices = new int[mqMesh.Vertices.Count];
            foreach (MqVertex vertex in mqMesh.Vertices)
            {
                positionIndices[vertex.Index] = buildingMesh.Positions.Count;
                buildingMesh.Positions.Add(vertex.Position);

                vertexToFaces.Add(new List<MqFace>());
            }

            // 三角形化
            triangles = new List<MqFace>(mqMesh.Faces.Count * 2);
            triangleIndices = new List<int>(mqMesh.Faces.Count * 6);
            foreach (MqFace face in mqMesh.Faces)
            {
                if (face.Vertices.Length == 2)
                    continue;

                // 面法線の計算。頂点法線の計算は三角形化後の面ではなく、元の面法線から計算する
                // ここでは頂点で計算した法線の平均値を使用している
                // これは四角形面の場合、非平面である可能性があるのがひとつ、
                // そして、法線計算では辺の長さによって誤差が発生するので
                // その誤差を軽減する為に各頂点から求めた法線の平均値を設定している
                MqVertex prevVtx = face.Vertices[face.Vertices.Length - 1];
                Vector3 faceNormal = Vector3.Zero;
                for (int i = 0; i < face.Vertices.Length; ++i)
                {
                    MqVertex curVtx = face.Vertices[i];
                    MqVertex nextVtx = (i < face.Vertices.Length - 1) ?
                        face.Vertices[i + 1] :
                        face.Vertices[0];

                    Vector3 n = Vector3.Cross(nextVtx.Position - curVtx.Position,
                                                curVtx.Position - prevVtx.Position);

                    if (n.LengthSquared() > 1e-12f)
                        faceNormal += Vector3.Normalize(n);

                    prevVtx = curVtx;
                }

                if (faceNormal.LengthSquared() > 1e-12f)
                    face.Normal = Vector3.Normalize(faceNormal);

                // 使用している面情報を追加する
                foreach (var vtx in face.Vertices)
                    vertexToFaces[positionIndices[vtx.Index]].Add(face);


                // 三角形情報の追加
                AddTriangle(face, 0, 1, 2);

                if (face.Vertices.Length == 4)
                    AddTriangle(face, 2, 3, 0);
            }

            // GeometryContentの生成
            MqObject mqObj = mqMesh.Owner;
            for (int triIdx = 0; triIdx < triangles.Count; ++triIdx)
            {
                MqFace face = triangles[triIdx];
                GeometryContent geometry = GetGeometry(face);

                int triVtxIdx = triIdx * 3;
                for (int i = 0; i < 3; ++i)
                {
                    int vtxIdx = triangleIndices[triVtxIdx + i];

                    // 頂点位置インデックスの追加
                    int posIdx = positionIndices[face.Vertices[vtxIdx].Index];
                    int currentIndex = geometry.Vertices.Add(posIdx);

                    int channelIdx = 0;
                    geometry.Vertices.Channels.Get<Vector3>(channelIdx++)[currentIndex] =
                        ComputeVertexNormal(mqObj, face, posIdx);

                    // SkinnedEffectはテクスチャ座標を必要とするので、ボーンウェイトがある
                    // 場合にもテクスチャ座標チャンネルを追加する
                    if (face.HasTexcoord || face.HasBoneWeights)
                    {
                        geometry.Vertices.Channels.
                            Get<Vector2>(channelIdx++)[currentIndex] =
                                                    face.Channels[vtxIdx].Texcoord;
                    }

                    if (face.HasVertexColor)
                    {
                        geometry.Vertices.Channels.Get<Color>(channelIdx++)[currentIndex] =
                                                        face.Channels[vtxIdx].Color;
                    }

                    if (face.HasBoneWeights)
                    {
                        geometry.Vertices.Channels.
                            Get<BoneWeightCollection>(channelIdx++)[currentIndex] =
                                                face.Channels[vtxIdx].BoneWeights;
                    }

                    geometry.Indices.Add(currentIndex);
                }
            }

            // 作業用フィールドをクリアする
            positionIndices = null;
            triangleIndices = null;
            triangles = null;
        }

        #region プライベートメソッド


        /// <summary>
        /// 三角形の追加
        /// </summary>
        private void AddTriangle(MqFace face, int idx0, int idx1, int idx2)
        {
            triangleIndices.Add(idx0);
            triangleIndices.Add(idx1);
            triangleIndices.Add(idx2);

            triangles.Add(face);
        }

        /// <summary>
        /// 頂点法線の計算
        /// </summary>
        Vector3 ComputeVertexNormal(MqObject mqObj, MqFace face, int vtxIdx)
        {
            if (!mqObj.SmoothAngle.HasValue) return face.Normal;

            // この頂点を使っている面法線の平均を頂点法線とする
            float smoothLimit = mqObj.SmoothLimit;
            float falloutPoint = mqObj.SmoothFallout;
            float ratioFactor = 1.0f / (1.0f - falloutPoint);

            Vector3 faceNormal = face.Normal;
            Vector3 normal = Vector3.Zero;
            int count = 0;
            foreach (var neighborFace in vertexToFaces[vtxIdx])
            {
                float dot = Vector3.Dot(faceNormal, neighborFace.Normal);
                if (dot > falloutPoint)
                {
                    // スムース制限角度との差によって各面法線に重みをつける
                    float t = 0.8f + (dot - falloutPoint) * ratioFactor;
                    t = MathHelper.Clamp(t, 0, 1);
                    t *= t;
                    normal += neighborFace.Normal * t;
                    count++;
                }
            }

            if (count == 0 || normal.LengthSquared() < 1e-8f)
                return faceNormal;

            normal.Normalize();
            return normal;
        }

        /// <summary>
        /// 指定された面を格納するためのGeometryContentの取得
        /// </summary>
        GeometryContent GetGeometry(MqFace face)
        {
            // マテリアルと頂点チャンネルからキーを生成する
            MaterialAndVertexChannelKey key = new MaterialAndVertexChannelKey(face);

            // このマテリアルと頂点チャンネルの組み合わせのGeometryContentはあるか？
            GeometryContent geometry;
            if (mapToGeometries.TryGetValue(key, out geometry))
            {
                // プリミティブ上限数を超えた場合にバッチを分割するか？
                // 16ビットの上限は65,535だが、GPUによっては65,535をストリップの
                // ストップインデックスとして扱っているものがあるので、安全な上限値にしている
                if (UseSixteenBitsIndex && geometry.Indices.Count >= 65530)
                {
                    mapToGeometries.Remove(key);
                    geometry = null;
                }
            }

            // マテリアルと頂点チャンネルからキーを生成する
            if (geometry == null)
            {
                geometry = new GeometryContent();
                mapToGeometries.Add(key, geometry);

                geometry.Material = face.Material;

                geometry.Vertices.Channels.Add(VertexChannelNames.Normal(0),
                                                    typeof(Vector3), null);

                if (face.HasTexcoord || face.HasBoneWeights)
                {
                    geometry.Vertices.Channels.Add(
                        VertexChannelNames.TextureCoordinate(0), typeof(Vector2), null);
                }

                if (face.HasVertexColor)
                {
                    geometry.Vertices.Channels.Add(VertexChannelNames.Color(0),
                                                                typeof(Color), null);
                }

                if (face.HasBoneWeights)
                {
                    geometry.Vertices.Channels.Add(VertexChannelNames.Weights(0),
                                                    typeof(BoneWeightCollection), null);
                }

                buildingMesh.Geometry.Add(geometry);
            }

            return geometry;
        }

        #endregion

        #region ヘルパークラス

        /// <summary>
        /// マテリアルと頂点チャンネルの組み合わせをキー値として使うためのクラス
        /// </summary>
        class MaterialAndVertexChannelKey : IEquatable<MaterialAndVertexChannelKey>
        {
            private MaterialContent material;
            private int channelKey;

            public MaterialAndVertexChannelKey(MqFace face)
            {
                this.material = face.Material;
                this.channelKey = GetVertexChannelKey(face);
            }

            /// <summary>
            /// 面の頂点チャンネル状態からキー値を取得する
            /// </summary>
            static int GetVertexChannelKey(MqFace face)
            {
                int key = face.HasTexcoord ? 1 : 0;
                key |= face.HasVertexColor ? 2 : 0;
                key |= face.HasBoneWeights ? 5 : 0;
                return key;
            }

            public bool Equals(MaterialAndVertexChannelKey other)
            {
                if (material != null && other.material != null)
                {
                    if (material.Equals(other.material) == false)
                    {
                        return false;
                    }
                }
                else if ((material == null && other.material != null) ||
                            (material != null && other.material == null))
                {
                    // 片方がnullで片方がnullじゃなかった
                    return false;
                }

                return channelKey == other.channelKey;
            }

            public override int GetHashCode()
            {
                int materialHash = 0;

                if (material != null)
                {
                    materialHash = material.GetHashCode();
                }

                return materialHash ^ channelKey.GetHashCode();
            }

        }

        #endregion

        #region フィールド

        // 生成中のメッシュ
        MeshContent buildingMesh;

        // マテリアルと頂点チャンネルをキーとした辞書
        Dictionary<MaterialAndVertexChannelKey, GeometryContent> mapToGeometries;

        // 各頂点から使用されている面へのリンク情報
        List<List<MqFace>> vertexToFaces;

        // 三角形リスト
        List<MqFace> triangles;

        // 三角形に使われているインデックスリスト
        List<int> triangleIndices;

        // MqVertex.IndexからPositionIndexへの変換テーブル
        int[] positionIndices;

        #endregion
    }
}
