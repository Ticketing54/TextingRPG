using System;
using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace TextingRPG.UI
{
    // 주사위 하나가 오른쪽으로 굴러가며 눈이 촤르륵 바뀌다가, 점점 느려지며 최종값에 정착하는 텍스트 연출.
    // 눈 표시는 diceLabel(TMP) 한 개로 처리하고, 실제 굴림 결과는 OnRollComplete로 알려준다.
    public class DiceRollAnimation : MonoBehaviour
    {
        [SerializeField] RectTransform dice;          // 굴러가는 주사위(이동/회전 대상)
        [SerializeField] TextMeshProUGUI diceLabel;   // 주사위 눈을 그리는 텍스트

        [Header("굴림 연출")]
        [SerializeField] float travelDistance = 360f;   // 왼쪽 시작점에서 오른쪽으로 굴러가는 거리(px)
        [SerializeField] float travelDuration = 0.9f;    // 굴러가는 시간
        [SerializeField] float spinDegrees = -720f;      // 굴러가는 동안 회전량(음수 = 오른쪽으로 구르는 방향)
        [SerializeField] float startSwapInterval = 0.03f;// 눈이 바뀌는 시작 간격(빠름)
        [SerializeField] float endSwapInterval = 0.14f;  // 정착 직전 눈이 바뀌는 간격(느림)
        [SerializeField] float settleHold = 0.35f;       // 최종값을 보여주고 멈춰 있는 시간

        [Header("주사위 눈")]
        [Tooltip("인덱스 0~5가 1~6. 폰트가 ⚀~⚅를 지원하지 않으면 \"1\"~\"6\" 등으로 교체.")]
        [SerializeField] string[] faces = { "⚀", "⚁", "⚂", "⚃", "⚄", "⚅" };

        // 굴림이 끝나면 최종 눈 값(1~6)을 전달한다.
        public event Action<int> OnRollComplete;

        public bool IsRolling { get; private set; }

        // 눈 값(1~6)에 해당하는 글리프. 결과를 다른 곳에 그대로 찍어둘 때 쓴다.
        public string FaceGlyph(int value)
        {
            int index = Mathf.Clamp(value - 1, 0, faces.Length - 1);
            return faces[index];
        }

        Vector2 _homePos;
        Quaternion _homeRot;
        bool _homeCaptured;
        Coroutine _routine;

        void Awake() => CaptureHome();

        void CaptureHome()
        {
            if (_homeCaptured || dice == null) return;
            _homePos = dice.anchoredPosition;
            _homeRot = dice.localRotation;
            _homeCaptured = true;
        }

        // forcedResult가 1~6이면 그 값으로 정착하고, 그 외에는 무작위.
        public void Roll(int forcedResult = -1)
        {
            if (IsRolling) return;
            CaptureHome();
            _routine = StartCoroutine(RollRoutine(forcedResult));
        }

        IEnumerator RollRoutine(int forcedResult)
        {
            IsRolling = true;

            int result = forcedResult >= 1 && forcedResult <= faces.Length
                ? forcedResult
                : UnityEngine.Random.Range(1, faces.Length + 1);

            dice.DOKill();
            dice.anchoredPosition = _homePos;
            dice.localRotation = _homeRot;
            dice.localScale = Vector3.one;
            gameObject.SetActive(true);

            // DOAnchorPosX(UI 모듈)는 이 어셈블리에서 안 보여서 코어 DOTween.To로 처리한다.
            float targetX = _homePos.x + travelDistance;
            DOTween.To(() => dice.anchoredPosition.x,
                    x => dice.anchoredPosition = new Vector2(x, dice.anchoredPosition.y),
                    targetX, travelDuration)
                .SetEase(Ease.OutCubic)
                .SetTarget(dice);
            dice.DOLocalRotate(
                    new Vector3(0f, 0f, _homeRot.eulerAngles.z + spinDegrees),
                    travelDuration,
                    RotateMode.FastBeyond360)
                .SetEase(Ease.OutCubic);

            float elapsed = 0f;
            while (elapsed < travelDuration)
            {
                diceLabel.text = faces[UnityEngine.Random.Range(0, faces.Length)];

                // 굴림 후반으로 갈수록 눈 교체 간격을 벌려서 "느려지다 멈추는" 느낌을 준다.
                float t = Mathf.Clamp01(elapsed / travelDuration);
                float interval = Mathf.Lerp(startSwapInterval, endSwapInterval, t * t);

                yield return new WaitForSeconds(interval);
                elapsed += interval;
            }

            diceLabel.text = faces[result - 1];
            dice.DOPunchScale(Vector3.one * 0.25f, 0.3f, 6, 0.8f);
            yield return new WaitForSeconds(settleHold);

            IsRolling = false;
            _routine = null;
            OnRollComplete?.Invoke(result);
        }

        void OnDisable()
        {
            if (_routine != null) StopCoroutine(_routine);
            _routine = null;
            IsRolling = false;
            if (dice != null) dice.DOKill();
        }
    }
}
