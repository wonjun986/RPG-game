using UnityEngine;

namespace Aethoria.Characters
{
    // 스킬 스프라이트 시트는 프레임마다 포즈가 달라 원본에서 캐릭터를 감싸는 크롭 크기가 제각각이다
    // (예: ASkill은 프레임 높이가 227~351px로 최대 1.55배 차이난다). PPU는 폴더 단위로 하나만
    // 적용되기 때문에, 보정 없이 그대로 재생하면 프레임이 바뀔 때마다 캐릭터가 갑자기 커지거나
    // 작아져 보인다. 재생 중인 프레임마다 실제 world 높이를 기준으로 Visual 트랜스폼의 스케일을
    // 보정해서, 항상 기준 키(targetHeight)로 보이게 맞춰준다.
    public static class SkillFrameNormalizer
    {
        public const float ReferenceCharacterHeight = 1.5f; // Necrosia 걷기 스프라이트 기준 실제 키

        public static void Apply(Transform visualTransform, Sprite frame, float targetHeight = ReferenceCharacterHeight)
        {
            if (frame == null) return;
            float nativeHeight = frame.bounds.size.y;
            if (nativeHeight <= 0f) return;

            float scale = targetHeight / nativeHeight;
            visualTransform.localScale = new Vector3(scale, scale, 1f);
        }

        // 걷기 프레임(머리~부츠 약 357px, 프레임 445px를 1.5로 맞춤)에서 보이는 캐릭터 몸 키.
        public const float WalkBodyHeight = 1.2f;

        // 한 시트에서 포즈마다 꽉 맞게 잘라 프레임 높이가 제각각이지만 캐릭터는 같은 크기로 그려진 시트(공격, A스킬)용.
        // 프레임 높이를 맞추면 짧게 잘린 프레임(웅크린 자세, 낫을 옆으로 휘두른 자세)일수록 캐릭터가 커져서,
        // 공격을 연타하거나 다른 동작으로 이어 쓰면 캐릭터가 갑자기 커 보였다. 대신 시트 전체에 같은 배율을 걸어
        // 캐릭터 몸 키(bodyPixelHeight, 원본 px)가 걷기와 같은 키로 보이게 한다.
        public static void ApplyBody(Transform visualTransform, Sprite frame, float bodyPixelHeight, float bodyTargetHeight = WalkBodyHeight)
        {
            if (frame == null || bodyPixelHeight <= 0f) return;
            float scale = bodyTargetHeight * frame.pixelsPerUnit / bodyPixelHeight;
            visualTransform.localScale = new Vector3(scale, scale, 1f);
        }

        public static void Reset(Transform visualTransform)
        {
            visualTransform.localScale = Vector3.one;
        }
    }
}
