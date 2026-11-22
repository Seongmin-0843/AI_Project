using System;
using System.Collections.Generic;

public enum Stamp { None, Approve, Reject }

// 진행 중인 거래 한 건
public class Transaction
{
    public TransactionTemplate Template;
    public Stamp Stamp = Stamp.None;
    public List<Finding> Reasons = new List<Finding>(); // 반려 사유 (반려 도장일 때만)

    // 값 치환 (기획서 7장): 서류에 들어갈 이름·번호와 오류용 값
    public string Name, Similar, Number, SwappedNumber;
    public bool LookedUp; // 단말기에서 조회함 → 서류 화면에 조회 기록 칸이 보임
    public string LookupNumber;     // 마지막으로 조회한 번호 (조회 기록의 '기억 번호' 칸)
    public Transaction WrongRecord; // 번호를 잘못 입력해 나온 다른 사람의 기록. 맞는 번호로 다시 조회하면 null
    public Transaction Record => WrongRecord ?? this; // 조회 기록 칸에 보이는 기록
    public bool Viewed;   // 서류를 한 번이라도 펼쳐 봄 (튜토리얼 안내용)

    // 기억 복원 (기획서 4-3)
    public MemorySet Memory;       // 복원 거래의 기억 세트
    public int[] CardOrder;        // 카드를 보여 주는 순서 (정답 순서가 아니게 섞음)
    public int RestoreFailures;    // 틀린 횟수. 3이면 자동 복원, 수익 0
    public bool Restored;          // 배열 성공
    public bool IsRestore => Template.Type == "Restore";
    public bool RestoreDone => Restored || RestoreFailures >= Scoring.MaxRestoreTries;

    public Transaction(TransactionTemplate t) => Template = t;

    // 템플릿이 정한 곳에만 오류 값을 넣는다 (기획서 7장)
    public string AppName => Template.HasError("NameMismatch", "Application") ? Similar : Name;
    public string ConName => Template.HasError("NameMismatch", "Contract") ? Similar : Name;
    public string AppNumber => Template.HasError("NumberMismatch", "Application") ? SwappedNumber : Number;
    public string ConNumber => Template.HasError("NumberMismatch", "Contract") ? SwappedNumber : Number;
    public string Owner => Template.HasError("OwnerMismatch", "Lookup") ? Similar : Name;
}

// 기획서 3장 지급 규칙과 오류 점수
public static class Scoring
{
    public static int Points(string kind)
    {
        switch (kind)
        {
            case "NameMismatch":
            case "NumberMismatch":
            case "SignMissing":
            case "DescMismatch":
                return 1; // 서류 대조
            case "OwnerMismatch":
            case "KindMismatch":
            case "AgeMismatch":
            case "Unregistered":
                return 2; // 기억 조회
            case "RestoreMismatch":
                return 3; // 기억 복원
            case "ConsentMissing":
            case "UnderTen":
                return 3; // 규정 위반
            default:
                throw new ArgumentException($"점수가 정해지지 않은 오류 종류: {kind}");
        }
    }

    // 이 오류를 찾았다고 인정되는 항목 조합 (Finding.Key와 같은 형식: 정렬 후 +로 연결)
    public static string[] ExpectedKeys(TemplateError e)
    {
        switch (e.Kind)
        {
            // 서명은 이름으로 적히므로 이름이 다르면 서명도 다르다 → 서명끼리, 이름↔서명 짝도 인정
            case "NameMismatch": return new[] { "Application.Name+Contract.Name", "Application.Sign+Contract.Sign", "Application.Name+Contract.Sign", "Application.Sign+Contract.Name" };
            // 조회 기록에도 번호가 적히므로 서류 번호 ↔ 조회 번호 짝도 인정
            case "NumberMismatch": return new[] { "Application.Number+Contract.Number", "Application.Number+Lookup.Number", "Contract.Number+Lookup.Number" };
            case "DescMismatch": return new[] { "Application.Desc+Contract.Desc" };
            case "SignMissing": return new[] { $"{e.Doc}.Sign" };
            // 신청인·계약자 이름은 같은 값이라 어느 쪽과 짝지어도 인정, 조회 기록 한 칸만 골라도 인정
            case "OwnerMismatch": return new[] { "Application.Name+Lookup.Owner", "Contract.Name+Lookup.Owner", "Lookup.Owner" };
            case "KindMismatch": return new[] { "Application.Kind+Lookup.Kind", "Lookup.Kind" };
            case "AgeMismatch": return new[] { "Application.Age+Lookup.Age", "Lookup.Age" };
            case "Unregistered": return new[] { "Lookup.Unregistered" };
            // 신청서·계약서 설명은 같은 문장이라 둘 다 인정, 복원된 기억 한 칸만 골라도 인정
            case "RestoreMismatch": return new[] { "Application.Desc+Restore.Result", "Contract.Desc+Restore.Result", "Restore.Result" };
            // 규정 위반: 규정 문장 + 근거가 되는 항목. 동의서는 '없음' 칸만, 또는 '없음' 칸 + 기억 저장 시기도 인정
            case "ConsentMissing": return new[] { "Application.Attachment+Rule.3", "Application.Age+Rule.3", "Lookup.Age+Rule.3", "Application.Age+Application.Attachment", "Application.Attachment+Lookup.Age", "Application.Attachment" };
            // 저장 시기 한 칸만 고르면 '기억 저장 시기 불일치'와 구별되지 않으므로 규정 문장이 꼭 있어야 한다
            case "UnderTen": return new[] { "Lookup.Age+Rule.4", "Application.Age+Rule.4" };
            default: throw new ArgumentException($"항목 조합이 정해지지 않은 오류 종류: {e.Kind}");
        }
    }

    public static bool Matches(TemplateError e, Finding f) => !f.Misread && Array.IndexOf(ExpectedKeys(e), f.Key) >= 0;

    // tray: 넣은 서류함 (Approve/Reject)
    public static int Calculate(Transaction tx, Stamp tray)
    {
        var t = tx.Template;
        if (tx.Stamp == Stamp.None || tx.Stamp != tray) return 0;            // 도장과 서류함이 다름
        if (tx.Stamp == Stamp.Approve && tx.WrongRecord != null) return 0;   // 다른 사람의 조회 기록을 보고 승인 (기억 번호 오입력)
        if (tx.Stamp == Stamp.Approve) return t.Answer == "Approve" ? RoundTo10(t.Reward * RestoreRate(tx)) : 0;

        if (t.Answer != "Reject" || tx.Reasons.Count == 0) return 0;         // 판정 틀림 / 사유 없음
        int found = 0, total = 0;
        foreach (var e in t.Errors)
        {
            total += Points(e.Kind);
            if (tx.Reasons.Exists(r => Matches(e, r))) found += Points(e.Kind);
        }
        foreach (var r in tx.Reasons)                                         // 틀린 사유가 하나라도 섞임
            if (!t.Errors.Exists(e => Matches(e, r))) return 0;

        return RoundTo10((double)t.Reward * found / total * RestoreRate(tx)); // 배율을 모두 곱한 뒤 한 번만 반올림
    }

    public const int MaxRestoreTries = 3;

    // 복원 거래: 첫 시도 성공 100%, 두 번째 이후 성공 50%, 3회 실패 0
    public static double RestoreRate(Transaction tx)
    {
        if (!tx.IsRestore) return 1.0;
        if (tx.RestoreFailures >= MaxRestoreTries) return 0.0;
        return tx.RestoreFailures == 0 ? 1.0 : 0.5;
    }

    public static string KindName(string kind)
    {
        switch (kind)
        {
            case "NameMismatch": return "이름 불일치";
            case "NumberMismatch": return "기억 번호 불일치";
            case "SignMissing": return "서명 누락";
            case "DescMismatch": return "기억 설명 불일치";
            case "OwnerMismatch": return "조회 소유자 불일치";
            case "KindMismatch": return "기억 종류 불일치";
            case "AgeMismatch": return "기억 저장 시기 불일치";
            case "Unregistered": return "등록되지 않은 번호";
            case "RestoreMismatch": return "복원한 기억 내용 불일치";
            case "ConsentMissing": return "보호자 동의서 누락";
            case "UnderTen": return "10세 미만의 기억";
            default: return kind;
        }
    }

    // 재도전 화면에 보여 줄 놓친 오류·실수 (기획서 6장)
    public static List<string> Missed(Transaction tx, Stamp tray)
    {
        var t = tx.Template;
        var list = new List<string>();
        if (tx.IsRestore && tx.RestoreFailures >= MaxRestoreTries) list.Add("기억 복원 3회 실패");
        if (tx.Stamp == Stamp.Approve ? tx.WrongRecord != null : tx.Reasons.Exists(r => r.Misread)) list.Add("기억 번호 오입력");
        if (tx.Stamp != tray) { list.Add("도장과 다른 서류함에 넣음"); return list; }
        if (tx.Stamp == Stamp.Reject && t.Answer == "Approve") { list.Add("정상 거래를 반려함"); return list; }

        foreach (var e in t.Errors)
            if (tx.Stamp == Stamp.Approve || !tx.Reasons.Exists(r => Matches(e, r)))
                list.Add(KindName(e.Kind));
        if (tx.Stamp == Stamp.Reject && tx.Reasons.Exists(r => !r.Misread && !t.Errors.Exists(e => Matches(e, r))))
            list.Add("틀린 반려 사유 포함");
        return list;
    }

    public static int RoundTo10(double v) => (int)(Math.Round(v / 10.0, MidpointRounding.AwayFromZero) * 10);
}
