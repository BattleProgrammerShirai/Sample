#region ファイル説明
//-----------------------------------------------------------------------------
// MqScene.cs
//=============================================================================
#endregion

#region Using ステートメント

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Content.Pipeline;
using Microsoft.Xna.Framework.Content.Pipeline.Graphics;
using Microsoft.Xna.Framework.Content.Pipeline.Serialization.Intermediate;

#endregion

namespace MetasequoiaPipeline
{
    /// <summary>
    /// メタセコイア用シーンデータ
    /// </summary>
    public class MqScene
    {
        #region 定数

        // ルートノード名
        public const string RootNodeName = "#Root";

        // スケルトンルートノード名、複数のルートボーンがある場合に生成される
        public const string SkeltonNodeName = "#Skelton";

        #endregion

        #region プロパティ

        /// <summary>
        /// XNAコンテントパイプラインDOM形式へ変更後のルートノードの取得
        /// </summary>
        public NodeContent Root { get; internal set; }

        /// <summary>
        /// 使用されているマテリアルリストの取得
        /// </summary>
        public IList<MaterialContent> Materials { get { return materials; } }

        [ContentSerializer(ElementName = "Materials", CollectionItemName = "Material")]
        List<MaterialContent> materials = new List<MaterialContent>();

        /// <summary>
        /// メタセコイア形式のオブジェクトリストの取得
        /// </summary>
        public IList<MqObject> Objects { get { return objects; } }

        [ContentSerializer(ElementName = "Objects", CollectionItemName = "Object")]
        List<MqObject> objects = new List<MqObject>();

        /// <summary>
        /// スケルトン情報の取得
        /// </summary>
        public IList<MqBone> Skelton { get { return skelton; } }

        [ContentSerializer(
            ElementName = "Skelton", CollectionItemName = "Bone", Optional = true)]
        public List<MqBone> skelton = new List<MqBone>();

        /// <summary>
        /// アニメーション情報の取得
        /// </summary>
        [ContentSerializer(Optional = true)]
        public AnimationContentDictionary Animations = new AnimationContentDictionary();

        #endregion

        /// <summary>
        /// 指定されたファイルからシーンデータを読み込む
        /// </summary>
        /// <param name="filename">ファイル名</param>
        /// <param name="context">読み込みコンテキスト</param>
        /// <returns>読み込んだシーンデータ</returns>
        public static MqScene CraeteFromFile(string filename, MqSceneReadContext context)
        {
            if (String.IsNullOrEmpty(filename))
                throw new ArgumentNullException("filename");

            if (context == null)
                throw new ArgumentNullException("settings");

            string ext = Path.GetExtension(filename);

            // シーンデータのデシリアライズ
            MqScene scene = null;
            switch (ext.ToLower())
            {
                case ".mqo":
                    scene = MqReader.ReadFromFile(filename, context);
                    break;
                case ".mkx":
                    using (XmlReader input = XmlReader.Create(filename))
                        scene = IntermediateSerializer.Deserialize<MqScene>(input, filename);
                    break;
                default:
                    throw new InvalidOperationException(String.Format(
                        CultureInfo.CurrentCulture, Resources.UnknownFileType, ext));
            }

            // デシリアライズしたシーンの実体化
            scene.Concretion(context);

            return scene;
        }


        #region インターナルメソッド

        /// <summary>
        /// 指定されたインデックスからマテリアルを取得する
        /// </summary>
        internal MaterialContent GetMaterial(int materialIdx)
        {
            if (materialIdx == -1)
            {
                // デフォルトマテリアル情報を生成する
                if (defaultMaterial == null)
                {
                    defaultMaterial = new BasicMaterialContent { Name = "Default" };
                    defaultMaterial.DiffuseColor = new Vector3(0.8f);
                    defaultMaterial.SpecularColor = Vector3.Zero;

                    Materials.Add(defaultMaterial);
                }

                return defaultMaterial;
            }

            return Materials[materialIdx];
        }

        /// <summary>
        /// 読み込んだデータの実体化してプロセッサーで使用する状態にする
        /// </summary>
        void Concretion(MqSceneReadContext context)
        {
            Root = new NodeContent { Name = MqScene.RootNodeName };
            var nameToNodes = new Dictionary<string, NodeContent>();

            // スケルトン、マテリアル、そしてオブジェクトの実体化
            ConcreteSkelton(nameToNodes, context);
            ConcreteMaterials(context);
            ConcreteObjects(nameToNodes, context);

            // インポートしたMqSceneデータをルートコンテントに保持する
            if (context.PreserveMqScene)
                Root.OpaqueData.Add("MqScene", this);
        }

        /// <summary>
        /// スケルトンの実体化
        /// </summary>
        void ConcreteSkelton(Dictionary<string, NodeContent> nameToNodes,
                                MqSceneReadContext context)
        {
            if (Skelton.Count == 0) return;

            // BoneNodeを生成し、
            foreach (MqBone bone in Skelton)
            {
                NodeContent node = new BoneContent();
                node.Name = bone.Name;
                node.Transform = bone.Transform;
                nameToNodes.Add(bone.Name, node);
            }

            // ツリー構造を生成する。
            foreach (MqBone bone in Skelton)
            {
                NodeContent parent = (!String.IsNullOrEmpty(bone.Parent)) ?
                                                    nameToNodes[bone.Parent] : Root;
                parent.Children.Add(nameToNodes[bone.Name]);
            }

            NodeContent skeltonRoot = Root.Children[0];

            // もしルートとなるボーンが複数ある場合はスケルトン用のルートを構築する
            if (Root.Children.Count > 1)
            {
                skeltonRoot = new BoneContent { Name = SkeltonNodeName };

                // ボーンノードをスケルトンルート下へ移動する
                while (Root.Children.Count > 0)
                {
                    NodeContent node = Root.Children[0];
                    Root.Children.Remove(node);
                    skeltonRoot.Children.Add(node);
                }

                Root.Children.Add(skeltonRoot);
            }

            // アニメーションをスケルトンノードに設定する
            foreach (var val in Animations)
                skeltonRoot.Animations.Add(val.Key, val.Value);
        }

        /// <summary>
        /// マテリアルの実体化
        /// </summary>
        void ConcreteMaterials(MqSceneReadContext context)
        {
            foreach (MaterialContent material in materials)
            {
                // 鏡面指数に0が設定されている場合、適当な数値で設定しなおす
                object spec;
                if (material.OpaqueData.TryGetValue("SpecularPower", out spec))
                {
                    if (spec is float && Math.Abs((float)spec) < 1e-5f)
                        material.OpaqueData["SpecularPower"] = 1.0f;
                }

                // 透明テクスチャ処理
                if (material.Textures.ContainsKey("AlphaTexture"))
                {
                    ExternalReference<TextureContent> texRef;
                    if (material.Textures.TryGetValue("Texture", out texRef))
                    {
                        // ベーステクスチャのOpaqueDataにアルファテクスチャ情報を追加する
                        // この情報を元にしてMqModelProcessorはベーステクスチャと
                        // アルファテクスチャの合成を行う
                        texRef.OpaqueData.Add("AlphaTexture",
                                                material.Textures["AlphaTexture"]);
                    }
                    else
                    {
                        // アルファテクスチャしか指定されていない場合の処理
                        material.Textures["Texture"] = material.Textures["AlphaTexture"];
                        material.Textures["Texture"].OpaqueData.Add(
                                                        "ProcessAsAlphaTexture", true);

                        material.Textures.Remove("AlphaTexture");
                    }
                }
            }
        }

        /// <summary>
        /// オブジェクトの実体化
        /// </summary>
        void ConcreteObjects(Dictionary<string, NodeContent> nameToNodes,
                                MqSceneReadContext context)
        {
            // 使用オブジェクト情報の生成
            var employObjs = objects.ToDictionary(o => o.Name, o => false);
            var nameToObjs = objects.ToDictionary(o => o.Name, o => o);

            foreach (MqObject obj in objects)
            {
                obj.Scene = this;

                if (context.ImportInvisibleObjects || obj.IsVisible)
                {
                    // このオブジェクトがコンバート対象であれば、
                    // 親オブジェクトもコンバート対象とする
                    MqObject o = obj;
                    do
                    {
                        employObjs[o.Name] = true;

                        if (String.IsNullOrEmpty(o.Parent))
                            o = null;
                        else
                            o = nameToObjs[o.Parent];

                    } while (o != null);
                }
            }

            // 使用するオブジェクトからノードを生成する
            foreach (var a in employObjs)
            {
                if (!a.Value) continue;
                MqObject obj = nameToObjs[a.Key];
                NodeContent node = (obj.Mesh == null) ?
                                new NodeContent() : new MeshContent();
                node.Name = obj.Name;
                nameToNodes.Add(obj.Name, node);
            }

            // 各オブジェクトの実体化
            foreach (var obj in objects)
            {
                if (employObjs[obj.Name] == false) continue;

                NodeContent node = nameToNodes[obj.Name];

                // 親子関係の設定
                NodeContent parent = (!String.IsNullOrEmpty(obj.Parent)) ?
                                                    nameToNodes[obj.Parent] : Root;
                parent.Children.Add(node);
                node.Transform = obj.Transform;

                if (context.PreserveMqScene) node.OpaqueData.Add("MqObject", obj);

                // このオブジェクトがアルファ値をもつ頂点カラーを使っているかの情報を
                // OpaqueDataに格納する
                node.OpaqueData.Add("HasAlphaVertexColor", obj.HasAlphaVertexColor);

                //-----------------------------
                // メッシュ専用の処理
                MqMesh mesh = obj.Mesh;
                if (mesh == null) continue;
                mesh.Owner = obj;

                if (obj.SmoothAngle.HasValue)
                {
                    float rad = obj.SmoothAngle.Value;
                    obj.SmoothLimit = (float)Math.Cos(rad);
                    // スムース処理終端値、スムース限界値のより１割大きく設定
                    obj.SmoothFallout = (float)Math.Cos(Math.Min(Math.PI, rad * 1.1));
                }

                // メタセコイアの頂点座標は全てワールド座標となっているので
                // ローカル座標へと変換する
                Matrix xform = Matrix.Invert(node.AbsoluteTransform);
                foreach (MqVertex vtx in mesh.Vertices)
                {
                    Vector3 pos = vtx.Position;
                    Vector3.Transform(ref pos, ref xform, out pos);
                    vtx.Position = pos;
                }

                // 各フェースのマテリアル情報をインデックスから取得
                foreach (MqFace face in mesh.Faces)
                    face.Material = GetMaterial(face.materialIdx);

                // ミラーリング、回転体を適用する
                MqMeshHelper.ApplyMirroring(mesh, node.AbsoluteTransform);
                MqMeshHelper.ApplyLathe(mesh, node.AbsoluteTransform);

                // Catmull-Clarkを適用する
                // #### 注意 ####
                // MqObject.Meshに設定されるのはサブディビジョン適用前のもの
                if (obj.PatchType == MqPatchType.CatmullClark)
                {
                    SubDivider subDivider = new SubDivider();
                    for (int i = 0; i < obj.PatchSegments; ++i)
                        mesh = subDivider.SubDivide(mesh);
                }

                // MeshContent情報の構築
                MqMeshBuilder meshBuilder = new MqMeshBuilder();
                meshBuilder.UseSixteenBitsIndex = context.UseSixteenBitsIndex;
                meshBuilder.BeginBuild((MeshContent)node);
                meshBuilder.AddMesh(mesh);
                meshBuilder.EndBuild();
            }
        }


        #endregion

        #region フィールド

        BasicMaterialContent defaultMaterial;

        #endregion

    }
}
