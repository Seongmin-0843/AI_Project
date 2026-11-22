using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// 메뉴 NUMB > Run Scoring Check: 기획서 3장 지급 규칙대로 금액이 나오는지 검사한다.
public static class ScoringCheck
{
    [MenuItem("NUMB/Run Scoring Check")]
    static void Run()
    {
        int fail = 0;
        void Expect(string name, int actual, int expected)
        {
            if (actual == expected) return;
            fail++;
            Debug.LogError($"[금액 검사 실패] {name}: {actual} (기대 {expected})");
        }

        var normal = T(200, "Approve");
        var oneErr = T(200, "Reject", E("NameMismatch", "Contract"));
        var twoErr = T(200, "Reject", E("NameMismatch", "Contract"), E("SignMissing", "Contract"));
        var threeErr = T(350, "Reject", E("NameMismatch", "Contract"), E("NumberMismatch", "Contract"), E("DescMismatch", "Contract"));

        const string name = "Application.Name+Contract.Name", number = "Application.Number+Contract.Number";
        const string sign = "Contract.Sign", wrong = "Application.Name+Contract.Number";

        // 모두 맞힘
        Expect("정상 거래 승인", Scoring.Calculate(Tx(normal, Stamp.Approve), Stamp.Approve), 200);
        Expect("오류 1개 반려", Scoring.Calculate(Tx(oneErr, Stamp.Reject, name), Stamp.Reject), 200);
        Expect("오류 2개 모두 찾음", Scoring.Calculate(Tx(twoErr, Stamp.Reject, name, sign), Stamp.Reject), 200);
        // 일부만 찾음
        Expect("오류 2개 중 1개", Scoring.Calculate(Tx(twoErr, Stamp.Reject, sign), Stamp.Reject), 100);
        Expect("350 거래 3개 중 2개 (233.3 → 230)", Scoring.Calculate(Tx(threeErr, Stamp.Reject, name, number), Stamp.Reject), 230);
        // 틀린 사유 섞임
        Expect("맞는 사유 + 틀린 사유", Scoring.Calculate(Tx(twoErr, Stamp.Reject, name, wrong), Stamp.Reject), 0);
        // 이름이 다르면 서명(이름으로 적힘)도 다르다 → 서명 쌍만, 이름 쌍과 함께, 이름↔서명도 인정
        Expect("이름 오류를 서명 쌍으로", Scoring.Calculate(Tx(oneErr, Stamp.Reject, "Application.Sign+Contract.Sign"), Stamp.Reject), 200);
        Expect("이름 쌍 + 서명 쌍", Scoring.Calculate(Tx(oneErr, Stamp.Reject, name, "Application.Sign+Contract.Sign"), Stamp.Reject), 200);
        Expect("신청인 ↔ 계약서 서명", Scoring.Calculate(Tx(oneErr, Stamp.Reject, "Application.Name+Contract.Sign"), Stamp.Reject), 200);
        Expect("사유 없음 반려", Scoring.Calculate(Tx(oneErr, Stamp.Reject), Stamp.Reject), 0);
        // 서류함 잘못 고름 / 판정 틀림
        Expect("반려 도장 + 승인함", Scoring.Calculate(Tx(oneErr, Stamp.Reject, name), Stamp.Approve), 0);
        Expect("승인 도장 + 반려함", Scoring.Calculate(Tx(normal, Stamp.Approve), Stamp.Reject), 0);
        Expect("오류 거래 승인", Scoring.Calculate(Tx(oneErr, Stamp.Approve), Stamp.Approve), 0);
        Expect("정상 거래 반려", Scoring.Calculate(Tx(normal, Stamp.Reject, name), Stamp.Reject), 0);
        Expect("도장 없이 분류", Scoring.Calculate(Tx(normal, Stamp.None), Stamp.Approve), 0);
        // M6 조회 오류 (2점): 소유자는 신청인·계약자 어느 쪽과 짝지어도 인정
        var owner = T(250, "Reject", E("OwnerMismatch", "Lookup"));
        Expect("소유자 ↔ 신청인", Scoring.Calculate(Tx(owner, Stamp.Reject, "Application.Name+Lookup.Owner"), Stamp.Reject), 250);
        Expect("소유자 ↔ 계약자", Scoring.Calculate(Tx(owner, Stamp.Reject, "Contract.Name+Lookup.Owner"), Stamp.Reject), 250);
        Expect("소유자 ↔ 기억 종류 (틀린 사유)", Scoring.Calculate(Tx(owner, Stamp.Reject, "Application.Kind+Lookup.Owner"), Stamp.Reject), 0);
        Expect("조회 소유자 한 칸만", Scoring.Calculate(Tx(owner, Stamp.Reject, "Lookup.Owner"), Stamp.Reject), 250);
        var kind = T(250, "Reject", E("KindMismatch", "Lookup"));
        Expect("조회 기억 종류 한 칸만", Scoring.Calculate(Tx(kind, Stamp.Reject, "Lookup.Kind"), Stamp.Reject), 250);
        Expect("소유자 오류에 기억 종류 한 칸 (틀린 사유)", Scoring.Calculate(Tx(owner, Stamp.Reject, "Lookup.Kind"), Stamp.Reject), 0);
        var age = T(250, "Reject", E("AgeMismatch", "Lookup"));
        Expect("기억 저장 시기: 신청서 + 조회", Scoring.Calculate(Tx(age, Stamp.Reject, "Application.Age+Lookup.Age"), Stamp.Reject), 250);
        Expect("기억 저장 시기: 조회 한 칸만", Scoring.Calculate(Tx(age, Stamp.Reject, "Lookup.Age"), Stamp.Reject), 250);
        Expect("기억 저장 시기 오류에 기억 종류 (틀린 사유)", Scoring.Calculate(Tx(age, Stamp.Reject, "Lookup.Kind"), Stamp.Reject), 0);
        var unreg = T(250, "Reject", E("Unregistered", "Lookup"));
        Expect("미등록 자동 기록", Scoring.Calculate(Tx(unreg, Stamp.Reject, "Lookup.Unregistered"), Stamp.Reject), 250);
        var mixed = T(300, "Reject", E("NameMismatch", "Contract"), E("KindMismatch", "Lookup"));
        Expect("이름(1) + 종류(2) 중 종류만 (200)", Scoring.Calculate(Tx(mixed, Stamp.Reject, "Application.Kind+Lookup.Kind"), Stamp.Reject), 200);
        Expect("이름(1) + 종류(2) 중 이름만 (100)", Scoring.Calculate(Tx(mixed, Stamp.Reject, name), Stamp.Reject), 100);

        // M6 복원 배율: 첫 시도 100%, 두 번째 이후 50%, 3회 실패 0 (배율을 모두 곱한 뒤 한 번만 반올림)
        var restoreOk = T(300, "Approve"); restoreOk.Type = "Restore";
        var restoreErr = T(300, "Reject", E("RestoreMismatch", "Restore")); restoreErr.Type = "Restore";
        const string restored = "Application.Desc+Restore.Result";
        Expect("복원 첫 시도 성공 + 승인", Scoring.Calculate(Rx(restoreOk, 0, Stamp.Approve), Stamp.Approve), 300);
        Expect("복원 두 번째 성공 + 승인", Scoring.Calculate(Rx(restoreOk, 1, Stamp.Approve), Stamp.Approve), 150);
        Expect("복원 세 번째 성공 + 승인", Scoring.Calculate(Rx(restoreOk, 2, Stamp.Approve), Stamp.Approve), 150);
        Expect("복원 3회 실패 + 맞는 판정", Scoring.Calculate(Rx(restoreOk, 3, Stamp.Approve), Stamp.Approve), 0);
        Expect("복원 불일치 반려 첫 시도", Scoring.Calculate(Rx(restoreErr, 0, Stamp.Reject, restored), Stamp.Reject), 300);
        Expect("복원 불일치 반려 재시도", Scoring.Calculate(Rx(restoreErr, 1, Stamp.Reject, restored), Stamp.Reject), 150);
        Expect("복원 불일치: 계약서 설명 + 복원된 기억", Scoring.Calculate(Rx(restoreErr, 0, Stamp.Reject, "Contract.Desc+Restore.Result"), Stamp.Reject), 300);
        Expect("복원 불일치: 복원된 기억 한 칸만", Scoring.Calculate(Rx(restoreErr, 0, Stamp.Reject, "Restore.Result"), Stamp.Reject), 300);
        Expect("복원 불일치: 신청서 이름 + 복원된 기억 (틀린 사유)", Scoring.Calculate(Rx(restoreErr, 0, Stamp.Reject, "Application.Name+Restore.Result"), Stamp.Reject), 0);
        var example = T(350, "Reject", E("NameMismatch", "Contract"), E("RestoreMismatch", "Restore")); example.Type = "Restore";
        Expect("기획서 예시 350 × 3/4 × 0.5 = 131.25 → 130", Scoring.Calculate(Rx(example, 1, Stamp.Reject, restored), Stamp.Reject), 130);

        // 기억 번호 오입력: 다른 사람의 조회 기록을 보고 승인하거나 그 기록을 사유로 들면 0, 서류만으로 찾은 사유는 그대로 인정
        Transaction Wrong(Transaction tx)
        {
            tx.WrongRecord = new Transaction(T(0, "Approve"));
            foreach (var r in tx.Reasons) r.Misread = r.UsesRecord;
            return tx;
        }
        Expect("오입력한 채 정상 거래 승인", Scoring.Calculate(Wrong(Tx(normal, Stamp.Approve)), Stamp.Approve), 0);
        Expect("오입력한 채 복원 거래 승인", Scoring.Calculate(Wrong(Rx(restoreOk, 0, Stamp.Approve)), Stamp.Approve), 0);
        Expect("오입력 기록을 사유로 반려", Scoring.Calculate(Wrong(Tx(owner, Stamp.Reject, "Lookup.Owner")), Stamp.Reject), 0);
        Expect("오입력했지만 서류 오류로 반려", Scoring.Calculate(Wrong(Tx(oneErr, Stamp.Reject, name)), Stamp.Reject), 200);
        var num = T(200, "Reject", E("NumberMismatch", "Contract"));
        Expect("번호 오류: 계약서 번호 + 조회 번호", Scoring.Calculate(Tx(num, Stamp.Reject, "Contract.Number+Lookup.Number"), Stamp.Reject), 200);

        // M7 규정 위반 (3점)
        var consent = T(350, "Reject", E("ConsentMissing", "Application"));
        foreach (var key in new[] { "Application.Attachment+Rule.3", "Lookup.Age+Rule.3", "Application.Attachment" })
            Expect($"동의서 누락: {key}", Scoring.Calculate(Tx(consent, Stamp.Reject, key), Stamp.Reject), 350);
        Expect("동의서 누락에 규정 4 (틀린 사유)", Scoring.Calculate(Tx(consent, Stamp.Reject, "Lookup.Age+Rule.4"), Stamp.Reject), 0);
        Expect("동의서 누락에 저장 시기 한 칸 (틀린 사유)", Scoring.Calculate(Tx(consent, Stamp.Reject, "Lookup.Age"), Stamp.Reject), 0);
        Expect("서류가 일치해도 규정 위반 승인은 0", Scoring.Calculate(Tx(consent, Stamp.Approve), Stamp.Approve), 0);
        var under = T(250, "Reject", E("UnderTen", "Lookup"));
        Expect("10세 미만: 규정 4 + 조회 저장 시기", Scoring.Calculate(Tx(under, Stamp.Reject, "Lookup.Age+Rule.4"), Stamp.Reject), 250);
        Expect("10세 미만: 저장 시기 한 칸 (틀린 사유)", Scoring.Calculate(Tx(under, Stamp.Reject, "Lookup.Age"), Stamp.Reject), 0);
        var ruleMixed = T(350, "Reject", E("NameMismatch", "Contract"), E("ConsentMissing", "Application"));
        Expect("기획서 예시: 동의서만 260", Scoring.Calculate(Tx(ruleMixed, Stamp.Reject, "Application.Attachment+Rule.3"), Stamp.Reject), 260);
        Expect("기획서 예시: 이름만 90", Scoring.Calculate(Tx(ruleMixed, Stamp.Reject, name), Stamp.Reject), 90);

        // 반올림
        Expect("반올림 87.5 → 90", Scoring.RoundTo10(87.5), 90);
        Expect("반올림 262.5 → 260", Scoring.RoundTo10(262.5), 260);

        // 놓친 오류 (재도전 화면)
        void ExpectList(string label, List<string> actual, string expected)
        {
            var joined = string.Join(",", actual);
            if (joined == expected) return;
            fail++;
            Debug.LogError($"[놓친 오류 검사 실패] {label}: {joined} (기대 {expected})");
        }
        ExpectList("오류 거래 승인", Scoring.Missed(Tx(twoErr, Stamp.Approve), Stamp.Approve), "이름 불일치,서명 누락");
        ExpectList("일부만 찾음", Scoring.Missed(Tx(twoErr, Stamp.Reject, sign), Stamp.Reject), "이름 불일치");
        ExpectList("틀린 사유 섞임", Scoring.Missed(Tx(oneErr, Stamp.Reject, name, wrong), Stamp.Reject), "틀린 반려 사유 포함");
        ExpectList("서류함 잘못", Scoring.Missed(Tx(oneErr, Stamp.Reject, name), Stamp.Approve), "도장과 다른 서류함에 넣음");
        ExpectList("정상 반려", Scoring.Missed(Tx(normal, Stamp.Reject), Stamp.Reject), "정상 거래를 반려함");
        ExpectList("모두 맞힘", Scoring.Missed(Tx(oneErr, Stamp.Reject, name), Stamp.Reject), "");
        ExpectList("오입력한 채 승인", Scoring.Missed(Wrong(Tx(normal, Stamp.Approve)), Stamp.Approve), "기억 번호 오입력");
        ExpectList("오입력 기록을 사유로 반려", Scoring.Missed(Wrong(Tx(owner, Stamp.Reject, "Lookup.Owner")), Stamp.Reject), "기억 번호 오입력,조회 소유자 불일치");
        ExpectList("복원 3회 실패", Scoring.Missed(Rx(restoreOk, 3, Stamp.Approve), Stamp.Approve), "기억 복원 3회 실패");

        // 번호 치환: 자리바꿈 결과는 원래와 달라야 하고, 같은 숫자 구성에 이웃한 두 자리만 바뀜
        var rng = new System.Random(1);
        var used = new HashSet<string>();
        for (int i = 0; i < 500; i++)
        {
            var n = Variation.RandomNumber(rng, used);
            var s = Variation.SwapDigits(n, rng);
            int diff = 0;
            for (int c = 0; c < n.Length; c++) if (n[c] != s[c]) diff++;
            var a = n.ToCharArray(); var b = s.ToCharArray();
            System.Array.Sort(a); System.Array.Sort(b);
            if (n.Length != 6 || !n.StartsWith("M-") || diff != 2 || new string(a) != new string(b))
            {
                fail++;
                Debug.LogError($"[번호 치환 검사 실패] {n} → {s}");
                break;
            }
        }
        if (used.Count != 500) { fail++; Debug.LogError("[번호 치환 검사 실패] 번호가 중복됨"); }

        // 실제 템플릿: 완벽하게 처리하면 수익 100%, 슬롯 안의 템플릿 수익이 같음, 최대 수익이 빚 이상 (기획서 3·7장)
        var templates = GameData.LoadTemplates();
        foreach (var t in templates)
        {
            var perfect = new Transaction(t) { Stamp = t.Answer == "Approve" ? Stamp.Approve : Stamp.Reject, Restored = true };
            foreach (var e in t.Errors) perfect.Reasons.Add(new Finding { Key = Scoring.ExpectedKeys(e)[0] });
            Expect($"템플릿 {t.Id} 완벽 처리", Scoring.Calculate(perfect, perfect.Stamp), t.Reward);
        }
        foreach (var s in GameData.LoadStages())
        {
            int max = 0;
            foreach (var slot in templates.FindAll(t => t.Stage == s.Stage && !t.Tutorial).GroupBy(t => t.Slot)) // 튜토리얼은 정산 밖
            {
                var rewards = slot.Select(t => t.Reward).Distinct().ToList();
                if (rewards.Count > 1) { fail++; Debug.LogError($"[템플릿 검사 실패] 스테이지 {s.Stage} 슬롯 {slot.Key}의 수익이 템플릿마다 다름"); }
                max += rewards[0];
            }
            if (max == 0) continue; // 아직 템플릿이 없는 스테이지
            if (max < s.Debt) { fail++; Debug.LogError($"[템플릿 검사 실패] 스테이지 {s.Stage} 최대 수익 {max} < 빚 {s.Debt}"); }
            else Debug.Log($"[템플릿] 스테이지 {s.Stage}: 최대 {max} / 빚 {s.Debt} (여유 {max - s.Debt})");
        }

        if (fail == 0) Debug.Log("[금액 검사] 모두 통과");
    }

    static TemplateError E(string kind, string doc) => new TemplateError { Kind = kind, Doc = doc };

    static TransactionTemplate T(int reward, string answer, params TemplateError[] errors) =>
        new TransactionTemplate { Id = "TEST", Reward = reward, Answer = answer, Errors = new List<TemplateError>(errors) };

    // 복원을 끝낸 거래 (failures < 3이면 성공, 3이면 자동 복원)
    static Transaction Rx(TransactionTemplate t, int failures, Stamp stamp, params string[] reasonKeys)
    {
        var tx = Tx(t, stamp, reasonKeys);
        tx.RestoreFailures = failures;
        tx.Restored = failures < Scoring.MaxRestoreTries;
        return tx;
    }

    static Transaction Tx(TransactionTemplate t, Stamp stamp, params string[] reasonKeys)
    {
        var tx = new Transaction(t) { Stamp = stamp };
        foreach (var k in reasonKeys) tx.Reasons.Add(new Finding { Key = k });
        return tx;
    }
}
