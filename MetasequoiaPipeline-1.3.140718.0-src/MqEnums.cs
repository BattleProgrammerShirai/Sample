#region ファイル説明
//-----------------------------------------------------------------------------
// MqEnums.cs
//
// インポート時に使用する列挙型の宣言
//=============================================================================
#endregion

#region Using ステートメント

using System;

#endregion

namespace MetasequoiaPipeline
{
    /// <summary>
    /// パッチの種類
    /// </summary>
    public enum MqPatchType
    {
        Polygon = 0,        // ポリゴン面
        Spline1 = 1,        // スプライン面１(未対応)
        Spline2 = 2,        // スプライン面２(未対応)
        CatmullClark = 3    // Catmull-Clark面
    }

    /// <summary>
    /// ミラーリングの種類
    /// </summary>
    public enum MqMirrorType
    {
        None = 0,       // ミラーリングなし
        Split = 1,      // 分割ミラーリング
        Connect = 2,    // 接続ミラーリング
    }

    /// <summary>
    /// ミラーリング軸の種類
    /// </summary>
    [Flags]
    public enum MqMirrorAxies
    {
        None = 0,   // なし
        X = 1,      // Ｘ軸
        Y = 2,      // Ｙ軸
        Z = 4,      // Ｚ軸
        Local = 8,  // ローカル座標でミラーリング  
    }

    /// <summary>
    /// 回転体の種類
    /// </summary>
    public enum MqLatheType
    {
        None = 0,       // なし
        DubleSided = 3  // 回転体を適用、両面
    }

    /// <summary>
    /// 回転体の軸
    /// </summary>
    public enum MqLatheAxis
    {
        X = 0,  // Ｘ軸
        Y = 1,  // Ｙ軸
        Z = 2   // Ｚ軸
    }

}