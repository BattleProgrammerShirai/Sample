#region ファイル説明
//-----------------------------------------------------------------------------
// MqModelProcessor.cs
//=============================================================================
#endregion

#region Using ステートメント

using System.Collections.Generic;
using Microsoft.Xna.Framework.Content.Pipeline;
using Microsoft.Xna.Framework.Content.Pipeline.Graphics;
using Microsoft.Xna.Framework.Content.Pipeline.Processors;

#endregion

namespace MetasequoiaPipeline
{
    /// <summary>
    /// メタセコイアモデル用プロセッサー
    /// </summary>
    /// <remarks>
    /// 基本的な処理はModelProcessorと変わりないが、アルファテクスチャ合成用の
    /// マテリアルプロセッサーを使用している。
    /// </remarks>
    [ContentProcessor(DisplayName = "メタセコイア モデルプロセッサー")]
    public class MqModelProcessor : ModelProcessor
    {
        /// <summary>
        /// モデルデータの処理
        /// </summary>
        public override ModelContent Process(NodeContent input,
                                                ContentProcessorContext context)
        {
            // ベースクラスのメソッドを呼ぶだけ……
            processedMaterials = new Dictionary<MaterialContent, MaterialContent>();
            ModelContent modelContent = base.Process(input, context);

            // と、思ったけどテストに使っているモデルの多くが半透明データを使用していたので
            // MeshParts.Tagに半透明かどうかの情報を入れる処理も追加。
            // 
            // ゲームで使用するアルファブレンドには、アルファテストのみで良いのか、
            // スムースエッジ処理の有無、または完全な半透明が必要なのかを選択する必要があり、
            // メタセコイア自体が提供する情報ではそれらを判断することはできない。
            // 本来はこういった情報はゲーム向けのレベルエディタ等で指定するのが好ましい。
            //
            // なのですが、簡易ビューアー的にはあった方が楽チンなのも事実。
            // アルファ値の有無チェックは軽くない処理なので、レベルエディタを使っている人は
            // この処理を省くと良いでしょう。
            ProcessAlphaUsage(modelContent, context);

            return modelContent;
        }

        /// <summary>
        /// マテリアル変換処理
        /// </summary>
        /// <remarks>単にメタセコイア用のマテリアルプロセッサーを呼び出しているだけ</remarks>
        protected override MaterialContent ConvertMaterial(MaterialContent material,
            ContentProcessorContext context)
        {
            // このマテリアルは既に処理済みか?
            if (!processedMaterials.ContainsKey(material))
            {
                // MaterialProcessor用のプロセッサーパラメーターを生成する
                OpaqueDataDictionary processorParameters = new OpaqueDataDictionary();
                processorParameters["DefaultEffect"] = DefaultEffect;
                processorParameters["ColorKeyColor"] = ColorKeyColor;
                processorParameters["ColorKeyEnabled"] = ColorKeyEnabled;
                processorParameters["TextureFormat"] = TextureFormat;
                processorParameters["GenerateMipmaps"] = GenerateMipmaps;
                processorParameters["PremultiplyTextureAlpha"] = PremultiplyTextureAlpha;
                processorParameters["ResizeTexturesToPowerOfTwo"] =
                    ResizeTexturesToPowerOfTwo;

                processedMaterials[material] =
                    context.Convert<MaterialContent, MaterialContent>(material,
                                            "MqMaterialProcessor", processorParameters);
            }

            return processedMaterials[material];
        }

        /// <summary>
        /// アルファ使用状況を調査する
        /// </summary>
        /// <remarks>
        /// ここではアルファ値を使っているMeshParts.TagにInt値(1)を設定する。
        /// 実行時にはModelMeshPart.Tagにnull以外が設定しているものがアルファ値を使っている
        /// ModelMeshPartと判断することができるので、
        /// 最初にアルファ値を使っていない物(Tag==null)を描画したあとに、
        /// アルファ値を使っている物(Tag!=null)を描画することで、手前の半透明部分を描画した後に
        /// 奥の不透明部分を描画できないという視覚的な問題を解決することができる。
        /// ただし、半透明同士の前後関係は無視していることに注意。
        /// </remarks>
        private void ProcessAlphaUsage(ModelContent modelContent,
                                            ContentProcessorContext context)
        {
            foreach (ModelMeshContent mesh in modelContent.Meshes)
            {
                bool hasAlphaVertexColor =
                    mesh.SourceMesh.OpaqueData.ContainsKey("HasAlphaVertexColor") &&
                    (bool)mesh.SourceMesh.OpaqueData["HasAlphaVertexColor"];

                foreach (ModelMeshPartContent meshPart in mesh.MeshParts)
                {
                    if (hasAlphaVertexColor ||
                        (meshPart.Material.OpaqueData.ContainsKey("HasAlphaValue") &&
                            (bool)meshPart.Material.OpaqueData["HasAlphaValue"])
                        )
                    {
                        SetAlphaUsage(meshPart, 1);
                    }
                }
            }
        }

        /// <summary>
        /// アルファ使用状況をMeashPartContent.Tagに格納する
        /// </summary>
        /// <remarks>
        /// 仮想メソッドなので、派生させることによって独自の処理を追加することができる
        /// </remarks>
        protected virtual void SetAlphaUsage(ModelMeshPartContent meshPart, int usage)
        {
            meshPart.Tag = usage;
        }

        #region フィールド

        Dictionary<MaterialContent, MaterialContent> processedMaterials;

        #endregion

    }

}