#region ファイル説明
//-----------------------------------------------------------------------------
// MqMaterialProcessor.cs
//=============================================================================
#endregion

#region Using ステートメント

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content.Pipeline;
using Microsoft.Xna.Framework.Content.Pipeline.Graphics;
using Microsoft.Xna.Framework.Content.Pipeline.Processors;

#endregion

namespace MetasequoiaPipeline
{
    /// <summary>
    /// メタセコイア用マテリアルプロセッサー
    /// </summary>
    /// <remarks>
    /// テクスチャのOpaqueDataによって透明テクスチャの合成処理と通常処理を仕分けし、
    /// アルファ値使用状況を検査する。
    /// </remarks>
    [ContentProcessor(DisplayName = "MqMaterialProcessor")]
    public class MqMaterialProcessor : MaterialProcessor
    {
        /// <summary>
        /// マテリアルの変換処理
        /// </summary>
        public override MaterialContent Process(MaterialContent input,
                                                    ContentProcessorContext context)
        {
            // ローカルフィールドの初期化
            processedTextures = new Dictionary<ExternalReference<TextureContent>, bool>();

            // このマテリアルはアルファ値を使っているか？
            hasAlphaValue = QueryAlphaUsage(input, context);

            // マテリアルの変換処理
            MaterialContent processedMaterial = base.Process(input, context);

            // 処理後のマテリアルのOpaqueDataにアルファ値使用状況を格納する
            processedMaterial.OpaqueData.Add("HasAlphaValue", hasAlphaValue);

            return processedMaterial;
        }

        /// <summary>
        /// テクスチャ生成
        /// </summary>
        protected override ExternalReference<TextureContent> BuildTexture(
           string textureName,
           ExternalReference<TextureContent> texture,
           ContentProcessorContext context)
        {
            // 模様テクスチャと透明テクスチャの合成
            if (texture.OpaqueData.ContainsKey("AlphaTexture"))
            {
                hasAlphaValue = true;

                OpaqueDataDictionary processorParameters = CreateProcessorParameters();
                processorParameters["AlphaTexture"] = texture.OpaqueData["AlphaTexture"];

                return context.BuildAsset<TextureContent, TextureContent>(texture,
                            "AlphaTextureProcessor", processorParameters, null, null);
            }

            // 透明テクスチャのみ
            if (texture.OpaqueData.ContainsKey("ProcessAsAlphaTexture"))
            {
                hasAlphaValue = true;

                OpaqueDataDictionary processorParameters = CreateProcessorParameters();
                return context.BuildAsset<TextureContent, TextureContent>(texture,
                            "AlphaTextureProcessor", processorParameters, null, null);
            }

            if (!hasAlphaValue)
                hasAlphaValue = QueryAlphaUsage(texture, context);

            // 通常の処理
            return base.BuildTexture(textureName, texture, context);
        }

        /// <summary>
        /// TextureProcessor用のパラメーターを生成する
        /// </summary>
        OpaqueDataDictionary CreateProcessorParameters()
        {
            OpaqueDataDictionary processorParameters = new OpaqueDataDictionary();
            processorParameters["ColorKeyColor"] = ColorKeyColor;
            processorParameters["ColorKeyEnabled"] = ColorKeyEnabled;
            processorParameters["TextureFormat"] = TextureFormat;
            processorParameters["PremultiplyAlpha"] = PremultiplyTextureAlpha;
            processorParameters["GenerateMipmaps"] = GenerateMipmaps;
            processorParameters["ResizeToPowerOfTwo"] = ResizeTexturesToPowerOfTwo;

            return processorParameters;
        }

        /// <summary>
        /// 指定されたマテリアルはアルファ値を使っているか？
        /// </summary>
        /// <remarks>
        /// 仮想メソッドなので、派生させることによって独自の処理を追加することができる
        /// </remarks>
        protected virtual bool QueryAlphaUsage(MaterialContent material,
                                                    ContentProcessorContext context)
        {
            // 浮動小数点の誤差を考慮した境界値
            const float alphaThreshold = 254.0f / 255.0f;

            // それぞれのビルトインマテリアルタイプか調べる
            if (material is BasicMaterialContent)
            {
                BasicMaterialContent mat = material as BasicMaterialContent;
                return mat.Alpha <= alphaThreshold;
            }
            else if (material is SkinnedMaterialContent)
            {
                SkinnedMaterialContent mat = material as SkinnedMaterialContent;
                return mat.Alpha <= alphaThreshold;
            }
            else if (material is DualTextureMaterialContent)
            {
                DualTextureMaterialContent mat = material as DualTextureMaterialContent;
                return mat.Alpha <= alphaThreshold;
            }
            else if (material is AlphaTestMaterialContent)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// 指定されたテクスチャはアルファ値を使っているか？
        /// </summary>
        /// <remarks>
        /// 仮想メソッドなので、派生させることによって独自の処理を追加することができる
        /// </remarks>
        protected virtual bool QueryAlphaUsage(ExternalReference<TextureContent> texture,
                                                    ContentProcessorContext context)
        {
            // このテクスチャは処理済みか？
            if (processedTextures.ContainsKey(texture))
                return processedTextures[texture];

            // アルファ値を含んでいないファイルフォーマットを使用しているか？
            string ext = Path.GetExtension(texture.Filename);
            if (String.Compare(ext, ".jpg", true) == 0 ||
                String.Compare(ext, ".bmp", true) == 0)
            {
                processedTextures.Add(texture, false);
                return false;
            }

            // テクスチャのアルファ値を調べる(重いよ～)
            TextureContent texContent =
                context.BuildAndLoadAsset<TextureContent, TextureContent>(
                                        texture, null, CreateProcessorParameters(), null);

            texContent.ConvertBitmapType(typeof(PixelBitmapContent<Color>));
            PixelBitmapContent<Color> tex =
                            texContent.Faces[0][0] as PixelBitmapContent<Color>;

            // ピクセル全走査
            bool result = false;
            for (int y = 0; y < tex.Height && result == false; ++y)
            {
                foreach (Color c in tex.GetRow(y))
                {
                    if (c.A < 255)
                    {
                        result = true;
                        break;
                    }
                }
            }

            // 検査結果を格納する
            processedTextures.Add(texture, result);

            return result;
        }

        #region フィールド

        // このマテリアルはアルファチャンネルを使用しているか？
        bool hasAlphaValue;

        // このマテリアルに使用されているテクスチャのアルファ値の使用状況
        Dictionary<ExternalReference<TextureContent>, bool> processedTextures;

        #endregion

    }
}
