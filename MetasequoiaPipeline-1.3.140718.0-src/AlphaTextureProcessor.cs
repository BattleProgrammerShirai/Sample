#region ファイル説明
//-----------------------------------------------------------------------------
// AlphaTextureProcessor.cs
//=============================================================================
#endregion

#region Using ステートメント

using System;
using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content.Pipeline;
using Microsoft.Xna.Framework.Content.Pipeline.Graphics;
using Microsoft.Xna.Framework.Content.Pipeline.Processors;

#endregion

namespace MetasequoiaPipeline
{
    /// <summary>
    /// アルファテクスチャプロセッサー
    /// 指定された模様テクスチャに透明テクスチャの値をアルファ値として合成するプロセッサー
    /// </summary>
    [ContentProcessor]
    public class AlphaTextureProcessor : TextureProcessor
    {
        /// <summary>
        /// アルファテクスチャを合成する
        /// </summary>
        public override TextureContent Process(TextureContent baseTexture,
                                                        ContentProcessorContext context)
        {
            // 合成処理に適したデータ形式にするためにプロセッサパラメータを保存して
            // パラメーターを書き換える
            TextureProcessorOutputFormat orgTextureFormat = TextureFormat;
            bool orgGenerateMipmaps = GenerateMipmaps;

            TextureFormat = TextureProcessorOutputFormat.Color;
            GenerateMipmaps = false;

            // 模様テクスチャの処理
            baseTexture = base.Process(baseTexture, context);

            // アルファテクスチャの処理
            TextureContent alphaTexture = baseTexture;
            if (context.Parameters.ContainsKey("AlphaTexture"))
            {
                ExternalReference<TextureContent> alphaTexRef =
                    (ExternalReference<TextureContent>)context.Parameters["AlphaTexture"];

                // テクスチャプロセッサー用のパラメーターを生成する
                OpaqueDataDictionary processorParameters = new OpaqueDataDictionary();
                processorParameters["ColorKeyColor"] = ColorKeyColor;
                processorParameters["ColorKeyEnabled"] = ColorKeyEnabled;
                processorParameters["TextureFormat"] = TextureFormat;
                processorParameters["GenerateMipmaps"] = GenerateMipmaps;
                processorParameters["ResizeToPowerOfTwo"] = ResizeToPowerOfTwo;

                alphaTexture = context.BuildAndLoadAsset<TextureContent, TextureContent>
                            (alphaTexRef, "TextureProcessor", processorParameters, null);
            }

            // それぞれのテクスチャのビットマップ情報を取得する
            PixelBitmapContent<Color> alphaBmp =
                (PixelBitmapContent<Color>)alphaTexture.Faces[0][0];
            PixelBitmapContent<Color> baseBmp =
                (PixelBitmapContent<Color>)baseTexture.Faces[0][0];

            if (baseBmp.Width != alphaBmp.Width || baseBmp.Height != alphaBmp.Height)
                throw new InvalidContentException(String.Format(
                    CultureInfo.CurrentCulture, Resources.TextureSizeMismatched,
                    baseTexture.Identity.SourceFilename,
                    alphaTexture.Identity.SourceFilename));


            // 合成後のビットマップの生成
            PixelBitmapContent<Color> combinedBmp =
                new PixelBitmapContent<Color>(baseBmp.Width, baseBmp.Height);

            // ピクセル毎の合成(乗算済みアルファと補間式アルファでは計算式が違う)
            if (PremultiplyAlpha)
            {
                // 乗算済みアルファ形式の合成
                for (int y = 0; y < baseBmp.Height; y++)
                    for (int x = 0; x < baseBmp.Width; x++)
                    {
                        Color color = baseBmp.GetPixel(x, y);
                        Color alpha = alphaBmp.GetPixel(x, y);
                        color *= (float)(alpha.R / 255.0f);
                        combinedBmp.SetPixel(x, y, color);
                    }
            }
            else
            {
                // 補間式アルファ形式の合成
                for (int y = 0; y < baseBmp.Height; y++)
                    for (int x = 0; x < baseBmp.Width; x++)
                    {
                        Color color = baseBmp.GetPixel(x, y);
                        Color alpha = alphaBmp.GetPixel(x, y);
                        color.A = (byte)((int)color.A * (int)alpha.R >> 8);
                        combinedBmp.SetPixel(x, y, color);
                    }
            }

            Texture2DContent combinedTexture = new Texture2DContent();
            combinedTexture.Faces[0] = combinedBmp;

            // ミップマップの生成
            if (orgGenerateMipmaps)
                combinedTexture.GenerateMipmaps(true);

            // テクスチャフォーマットの変換
            if (orgTextureFormat == TextureProcessorOutputFormat.Color)
                combinedTexture.ConvertBitmapType(typeof(PixelBitmapContent<Color>));
            else
                combinedTexture.ConvertBitmapType(typeof(Dxt5BitmapContent));

            return combinedTexture;
        }

    }
}