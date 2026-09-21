using UnityEngine;
using UnityEngine.UI;

namespace Aethoria.UI
{
    // UGUI의 Image.Type.Filled는 sprite가 비어 있으면 fillAmount를 무시하고 그냥 꽉 찬 사각형으로
    // 그려버린다(공식 사양). 체력바/쿨타임 오버레이처럼 Filled를 쓰는 모든 Image가 공유해서 쓸
    // 흰색 1x1 스프라이트를 여기서 하나만 만들어 재사용한다.
    public static class UISprites
    {
        private static Sprite whitePixel;

        public static Sprite WhitePixel
        {
            get
            {
                if (whitePixel == null)
                {
                    var texture = new Texture2D(1, 1) { filterMode = FilterMode.Point };
                    texture.SetPixel(0, 0, Color.white);
                    texture.Apply();
                    whitePixel = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
                }
                return whitePixel;
            }
        }

        // Filled 타입 Image에 흰 픽셀 스프라이트를 붙여주는 편의 메서드.
        public static void UseAsFilled(this Image image)
        {
            image.sprite = WhitePixel;
        }
    }
}
