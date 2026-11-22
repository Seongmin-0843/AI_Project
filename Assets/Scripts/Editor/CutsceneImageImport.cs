using UnityEditor;

// Resources/Cutscenes의 컷씬 이미지와 Resources/Portraits의 초상화는 원본 크기 그대로 가져온다.
// 기본 설정(2의 거듭제곱으로 늘림)이면 1672x941이 2048x1024가 되어 비율이 틀어진다.
public class CutsceneImageImport : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        var path = assetPath.Replace('\\', '/');
        if (!path.Contains("/Resources/Cutscenes/") && !path.Contains("/Resources/Portraits/")) return;
        var importer = (TextureImporter)assetImporter;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.alphaIsTransparency = path.Contains("/Resources/Portraits/"); // 투명 배경 가장자리에 잡색이 번지지 않게
        importer.mipmapEnabled = path.Contains("도장자국") || path.Contains("업무결과_승인") || path.Contains("업무결과_반려"); // 도장은 작게 그려서 밉맵 필요. 나머지는 화면에 꽉 차게 보여 주는 이미지라 밉맵이 필요 없음
        if (path.Contains("/Resources/Portraits/")) importer.textureCompression = TextureImporterCompression.Uncompressed; // 가는 선이 압축으로 깨지지 않게
        importer.maxTextureSize = 4096;  // 2048보다 큰 이미지가 들어와도 줄어들지 않게
    }
}
