namespace TextingRPG.Core
{
    public enum DiceOutcome
    {
        CriticalFailure, // 2
        Failure,         // 3-6
        Partial,         // 7-9
        Success,         // 10-11
        CriticalSuccess  // 12
    }

    // 6면체 주사위 두 개를 굴린 결과. 두 눈을 각각(A, B) 보여주고, 합(Total)으로 등급을 판정한다.
    public readonly struct DiceRollResult
    {
        public readonly int A;
        public readonly int B;

        public DiceRollResult(int a, int b)
        {
            A = a;
            B = b;
        }

        public int Total => A + B;
    }

    // 2d6를 굴려 결과의 "방향"을 정한다. 성패를 스탯으로 판정하지 않고,
    // 나온 등급을 프롬프트 문장으로 번역해 LLM에게 방향만 넘긴다.
    public static class DiceRoll
    {
        // 실제 게임용. 주사위 두 개를 각각 굴린다.
        public static DiceRollResult Roll()
        {
            return new DiceRollResult(UnityEngine.Random.Range(1, 7), UnityEngine.Random.Range(1, 7));
        }

        // 순수 함수: 2d6 합을 5단계 등급으로.
        // 기본(위험): 2 대실패 / 3-6 실패 / 7-9 부분 / 10-11 성공 / 12 대성공.
        // 무모(reckless): 크리티컬 구간이 넓어지고 중간이 좁아져 성패가 더 크게 갈린다 —
        //   2-3 대실패 / 4-6 실패 / 7-8 부분 / 9-10 성공 / 11-12 대성공.
        public static DiceOutcome Bucket(int total, bool reckless = false)
        {
            if (reckless)
            {
                if (total <= 3) return DiceOutcome.CriticalFailure;
                if (total <= 6) return DiceOutcome.Failure;
                if (total <= 8) return DiceOutcome.Partial;
                if (total <= 10) return DiceOutcome.Success;
                return DiceOutcome.CriticalSuccess;
            }

            if (total <= 2) return DiceOutcome.CriticalFailure;
            if (total <= 6) return DiceOutcome.Failure;
            if (total <= 9) return DiceOutcome.Partial;
            if (total <= 11) return DiceOutcome.Success;
            return DiceOutcome.CriticalSuccess;
        }
    }
}
