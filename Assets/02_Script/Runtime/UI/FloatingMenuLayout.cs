namespace TextingRPG.UI
{
    // 원형 메뉴 버튼의 스냅/펼침 방향을 결정하는 순수 계산. Unity 타입에 의존하지 않아 EditMode에서 바로 테스트한다.
    public static class FloatingMenuLayout
    {
        // 드래그를 놓았을 때 스냅할 x좌표(부모 기준 anchoredPosition.x).
        // 화면 중앙보다 왼쪽이면 왼쪽 가장자리로, 아니면(중앙 포함) 오른쪽 가장자리로.
        public static float SnapTargetX(float currentX, float parentWidth, float buttonRadius, float edgeMargin)
        {
            float edgeX = parentWidth / 2f - buttonRadius - edgeMargin;
            return currentX < 0f ? -edgeX : edgeX;
        }

        // 펼칠 때 하위 버튼이 이동할 방향 부호.
        // 화면 중앙보다 왼쪽이면 +1(오른쪽으로), 아니면(중앙 포함) -1(왼쪽으로) — 항상 화면 안쪽을 향한다.
        public static int ExpandDirectionSign(float currentX) => currentX < 0f ? 1 : -1;
    }
}
