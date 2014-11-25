#region ファイル説明
//-----------------------------------------------------------------------------
// MqMeshSerializer.cs
//=============================================================================
#endregion

#region Using ステートメント

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Xml;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Content.Pipeline.Graphics;
using Microsoft.Xna.Framework.Content.Pipeline.Serialization.Intermediate;

#endregion

namespace MetasequoiaPipeline
{
    /// <summary>
    /// MqMeshオブジェクトのシリアライザー
    /// デフォルトのシリアライザーだとメッシュデータ部分のサイズが大きくなるので
    /// 独自のシリアライザーを用いることでファイルサイズを小さくしている
    /// </summary>
    [ContentTypeSerializer]
    class MqMeshSerializer : ContentTypeSerializer<MqMesh>
    {
        #region シリアライズ

        /// <summary>
        /// MqMeshのシリアライズ
        /// </summary>
        protected override void Serialize(IntermediateWriter output, MqMesh value,
                                            ContentSerializerAttribute format)
        {
            if (value.Owner == null) return;

            // マテリアルコンテントオブジェクトから
            // マテリアルインデックスへの変換マップを生成する
            MqScene scene = value.Owner.Scene;
            var materialIdxMap = new Dictionary<MaterialContent, int>();
            for (int i = 0; i < scene.Materials.Count; ++i)
                materialIdxMap.Add(scene.Materials[i], i);

            XmlWriter xml = output.Xml;

            // 頂点データのシリアライズ
            xml.WriteStartElement("Vertices");
            for (int i = 0; i < value.Vertices.Count; ++i)
            {
                Vector3 pos = value.Vertices[i].Position;
                if (i != 0) xml.WriteWhitespace(" ");
                xml.WriteString(String.Format("{0} {1} {2}", pos.X, pos.Y, pos.Z));
            }
            xml.WriteEndElement();

            // 面データのシリアライズ
            StringBuilder text = new StringBuilder(1024);
            bool hasTexcoord = false, hasVertexColor = false, hasBoneWeights = false;

            xml.WriteStartElement("Faces");
            for (int faceIdx = 0; faceIdx < value.Faces.Count; ++faceIdx)
            {
                MqFace face = value.Faces[faceIdx];

                text.Length = 0;
                if (faceIdx != 0) text.Append(" / ");

                if (face.Material == null)
                    text.Append("-1");
                else
                    text.Append(materialIdxMap[face.Material]);

                foreach (MqVertex vtx in face.Vertices)
                {
                    text.Append(' ');
                    text.Append(vtx.Index);
                }

                xml.WriteString(text.ToString());

                if (face.HasTexcoord) hasTexcoord = true;
                if (face.HasVertexColor) hasVertexColor = true;
                if (face.HasBoneWeights) hasBoneWeights = true;
            }
            xml.WriteEndElement();

            // 頂点チャンネルのシリアライズ
            if (hasTexcoord || hasVertexColor || hasBoneWeights)
            {
                xml.WriteStartElement("VertexChannels");

                if (hasTexcoord) WriteTextureCoordinates(xml, value);
                if (hasVertexColor) WriteVertexColors(xml, value);
                if (hasBoneWeights) WriteBoneWeights(xml, value);

                xml.WriteEndElement(); // </VertexChannels>
            }
        }

        /// <summary>
        /// テクスチャ座標のシリアライズ
        /// </summary>
        void WriteTextureCoordinates(XmlWriter xml, MqMesh value)
        {
            StringBuilder text = new StringBuilder(1024);

            xml.WriteStartElement("VertexChannel");
            xml.WriteAttributeString("Name", "TextureCoordinate0");
            xml.WriteAttributeString("ElementType", "Framework:Vector2");

            for (int faceIdx = 0; faceIdx < value.Faces.Count; ++faceIdx)
            {
                MqFace face = value.Faces[faceIdx];
                text.Length = 0;
                if (faceIdx != 0) text.Append(" / ");

                if (face.HasTexcoord)
                {
                    for (int i = 0; i < face.Channels.Length; ++i)
                    {
                        MqVertexChannel ch = face.Channels[i];
                        if (i != 0) text.Append(' ');
                        text.Append(ch.Texcoord.X); text.Append(' ');
                        text.Append(ch.Texcoord.Y);
                    }
                }

                xml.WriteString(text.ToString());
            }
            xml.WriteEndElement();

        }

        /// <summary>
        /// 頂点カラーのシリアライズ
        /// </summary>
        void WriteVertexColors(XmlWriter xml, MqMesh value)
        {
            StringBuilder text = new StringBuilder(1024);

            xml.WriteStartElement("VertexChannel");
            xml.WriteAttributeString("Name", "Color0");
            xml.WriteAttributeString("ElementType", "Framework:Color");

            for (int faceIdx = 0; faceIdx < value.Faces.Count; ++faceIdx)
            {
                MqFace face = value.Faces[faceIdx];
                text.Length = 0;
                if (faceIdx != 0) text.Append(" / ");

                if (face.HasTexcoord)
                {
                    for (int i = 0; i < face.Channels.Length; ++i)
                    {
                        MqVertexChannel ch = face.Channels[i];
                        if (i != 0) text.Append(' ');
                        text.AppendFormat("{0:x8}", ch.Color.PackedValue);
                    }
                }

                xml.WriteString(text.ToString());
            }
            xml.WriteEndElement();

        }

        /// <summary>
        /// ボーンウェイトのシリアライズ
        /// </summary>
        void WriteBoneWeights(XmlWriter xml, MqMesh value)
        {
            xml.WriteStartElement("VertexChannel");
            xml.WriteAttributeString("Name", "Weights0");
            xml.WriteAttributeString("ElementType", "Graphics:BoneWeightCollection");

            // 使用されているボーンリストのシリアライズ
            var boneIndices = new Dictionary<string, int>();
            foreach (MqFace face in value.Faces)
            {
                foreach (MqVertexChannel channel in face.Channels)
                {
                    if (channel.BoneWeights == null) continue;
                    foreach (BoneWeight boneWeight in channel.BoneWeights)
                    {
                        string boneName = boneWeight.BoneName;

                        // このボーン名は登録済みか？
                        if (boneIndices.ContainsKey(boneName))
                            continue;

                        // 最初のボーンだったらBonesXMLエレメントを書き出す
                        if (boneIndices.Count == 0)
                            xml.WriteStartElement("Bones");

                        // ボーン名のシリアライズ
                        xml.WriteElementString("Bone", boneName);

                        boneIndices.Add(boneName, boneIndices.Count);
                    }
                }
            }

            if (boneIndices.Count > 0)
                xml.WriteEndElement(); // </Bones>

            // 各頂点のボーンウェイトを書き出す
            StringBuilder text = new StringBuilder(2048);
            xml.WriteStartElement("Weights");
            for (int faceIdx = 0; faceIdx < value.Faces.Count; ++faceIdx)
            {
                MqFace face = value.Faces[faceIdx];
                text.Length = 0;

                if (faceIdx != 0) text.Append(" / ");

                for (int chIdx = 0; chIdx < face.Channels.Length; ++chIdx)
                {
                    if (chIdx != 0) text.Append(" / ");
                    BoneWeightCollection boneWeights = face.Channels[chIdx].BoneWeights;

                    if (boneWeights != null)
                    {
                        for (int i = 0; i < boneWeights.Count; ++i)
                        {
                            if (i != 0) text.Append(' ');
                            BoneWeight boneWeight = boneWeights[i];
                            text.Append(boneIndices[boneWeight.BoneName]);
                            text.Append(' ');
                            text.Append(boneWeight.Weight);
                        }
                    }
                }
                xml.WriteString(text.ToString());
            }

            xml.WriteEndElement(); // </Weights>
            xml.WriteEndElement(); // </VertexChannel>

        }

        #endregion

        #region デシリアライズ

        /// <summary>
        /// MqMeshのデシリアライズ
        /// </summary>
        protected override MqMesh Deserialize(IntermediateReader input,
                        ContentSerializerAttribute format, MqMesh existingInstance)
        {
            if (input == null)
                throw new ArgumentNullException("input");

            MqMesh mesh = new MqMesh();

            // 頂点データのデシリアライズ
            if (input.MoveToElement("Vertices"))
            {
                input.Xml.ReadStartElement();

                XmlListReader listReader = new XmlListReader(input);
                while (!listReader.AtEnd)
                    mesh.AddPosition(listReader.ReadAsVector3());

                input.Xml.ReadEndElement();
            }

            // 面データのデシリアライズ
            if (input.MoveToElement("Faces"))
            {
                input.Xml.ReadStartElement(); // <Faces>

                XmlListReader listReader = new XmlListReader(input);
                int numIndices = -1;
                int matIdx = -1;
                MqFace face = null;
                int[] indices = new int[4];

                while (!listReader.AtEnd)
                {
                    if (listReader.PeekString() == "/")
                    {
                        listReader.ReadAsString();
                        face = mesh.AddFace(indices, numIndices);
                        face.materialIdx = matIdx;
                        matIdx = -1; numIndices = -1;
                    }

                    if (numIndices < 0)
                        matIdx = listReader.ReadAsInt32();
                    else
                        indices[numIndices] = listReader.ReadAsInt32();

                    numIndices++;
                }

                face = mesh.AddFace(indices, numIndices);
                face.materialIdx = matIdx;

                input.Xml.ReadEndElement(); // </Faces>
            }

            // 頂点チャンネルのデシリアライズ
            if (input.MoveToElement("VertexChannels"))
            {
                input.Xml.ReadStartElement(); // <VertexChannels>

                while (input.MoveToElement("VertexChannel"))
                {
                    if (!input.Xml.MoveToAttribute("Name"))
                        throw new InvalidOperationException(Resources.NoVertexChannelName);

                    string name = input.Xml.ReadContentAsString();
                    input.Xml.MoveToElement();

                    input.Xml.ReadStartElement();
                    switch (name.ToLower())
                    {
                        case "texturecoordinate0": ReadTexCoords(input, mesh); break;
                        case "color0": ReadVertexColors(input, mesh); break;
                        case "weights0": ReadBoneWeights(input, mesh); break;
                        default:
                            throw new InvalidOperationException(String.Format(
                                CultureInfo.CurrentCulture,
                                Resources.UnknownVertexChannel, name));
                    }
                    input.Xml.ReadEndElement();
                }

                input.Xml.ReadEndElement(); // </VertexChannels>
            }

            return mesh;
        }

        /// <summary>
        /// テクスチャ座標のデシリアライズ
        /// </summary>
        private void ReadTexCoords(IntermediateReader input, MqMesh mesh)
        {
            XmlListReader listReader = new XmlListReader(input);
            int faceIdx = 0, vtxIdx = 0;
            while (!listReader.AtEnd)
            {
                if (listReader.PeekString() == "/")
                {
                    listReader.ReadAsString();
                    faceIdx++; vtxIdx = 0;

                    continue;
                }

                MqFace face = mesh.Faces[faceIdx];
                face.Channels[vtxIdx++].Texcoord = listReader.ReadAsVector2();
                face.HasTexcoord = true;
            }
        }

        /// <summary>
        /// 頂点カラーのデシリアライズ
        /// </summary>
        private void ReadVertexColors(IntermediateReader input, MqMesh mesh)
        {
            XmlListReader listReader = new XmlListReader(input);
            int faceIdx = 0, vtxIdx = 0;
            while (!listReader.AtEnd)
            {
                if (listReader.PeekString() == "/")
                {
                    listReader.ReadAsString();
                    faceIdx++; vtxIdx = 0;

                    continue;
                }

                MqFace face = mesh.Faces[faceIdx];
                face.Channels[vtxIdx++].Color = listReader.ReadAsColor();
                face.HasVertexColor = true;
            }
        }

        /// <summary>
        /// ボーンウェイトのデシリアライズ
        /// </summary>
        private void ReadBoneWeights(IntermediateReader input, MqMesh mesh)
        {
            // ボーン名リストの読み込み
            List<string> boneNames = new List<string>();
            if (input.MoveToElement("Bones"))
            {
                input.Xml.ReadStartElement();
                while (input.MoveToElement("Bone"))
                    boneNames.Add(input.Xml.ReadElementContentAsString());

                input.Xml.ReadEndElement();
            }

            // ボーンウェイトの読み込み
            if (input.MoveToElement("Weights"))
            {
                input.Xml.ReadStartElement();

                XmlListReader listReader = new XmlListReader(input);
                Lazy<BoneWeightCollection> weights = new Lazy<BoneWeightCollection>();

                int faceIdx = 0, vtxIdx = 0;
                while (!listReader.AtEnd)
                {
                    if (listReader.PeekString() == "/")
                    {
                        listReader.ReadAsString(); // '/' 部分を読み飛ばす

                        MqFace face = mesh.Faces[faceIdx];
                        if (weights.IsValueCreated)
                        {
                            face.Channels[vtxIdx].BoneWeights = weights.Value;
                            face.HasBoneWeights = true;
                        }

                        if (++vtxIdx >= face.Channels.Length)
                        {
                            faceIdx++; vtxIdx = 0;
                        }

                        weights = new Lazy<BoneWeightCollection>();

                        continue;
                    }

                    int boneIdx = listReader.ReadAsInt32();
                    float boneWeight = listReader.ReadAsSingle();
                    weights.Value.Add(new BoneWeight(boneNames[boneIdx], boneWeight));
                }

                // 最後のボーンウェイトを追加するのを忘れずに
                if (weights.IsValueCreated)
                    mesh.Faces[faceIdx].Channels[vtxIdx].BoneWeights = weights.Value;

                input.Xml.ReadEndElement();
            }
        }

        #endregion

    }
}
