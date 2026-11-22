using UnityEditor;
using UnityEngine;

namespace MouthCode.EditorTools
{
    /// <summary>
    /// 게임 폰트 가져오기 설정을 고정한다.
    /// 갈무리(픽셀체)는 안티앨리어싱 없이 또렷하게, 검은고딕(제목용)은 부드럽게.
    /// 없는 글자(기호 등)는 OS의 맑은 고딕으로 대체.
    /// </summary>
    public class MouthCodeFontImport : AssetPostprocessor
    {
        void OnPreprocessAsset()
        {
            if (!(assetImporter is TrueTypeFontImporter f)) return;
            if (!assetPath.Replace('\\', '/').Contains("/MouthCode/Resources/Fonts/")) return;

            bool pixel = assetPath.Contains("Galmuri");
            f.fontTextureCase = FontTextureCase.Dynamic;
            f.includeFontData = true;
            f.fontRenderingMode = pixel ? FontRenderingMode.HintedRaster : FontRenderingMode.Smooth;
            f.ascentCalculationMode = AscentCalculationMode.FaceAscender;
            f.characterPadding = 1;
            f.fontNames = pixel
                ? new[] { "Galmuri11", "Malgun Gothic", "Segoe UI Symbol" }
                : new[] { "Black Han Sans", "Malgun Gothic", "Segoe UI Symbol" };
        }
    }
}
