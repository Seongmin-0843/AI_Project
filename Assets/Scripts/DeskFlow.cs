using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

// 하루(스테이지) 진행: 업무 지시문 → 거래 N건 (서류 → 도장 → 서류함) → 결과 화면 → 통과/재도전.
public class DeskFlow : MonoBehaviour
{
    public DocumentView view;
    public FindingPanel findings;
    public ReasonDialog reasonDialog;
    public MessagePanel directive, result;
    [Tooltip("도장 전 할 일이 남았을 때의 작은 팝업 (NUMB > Add Missing Objects). 비어 있으면 업무 지시문 창을 쓴다")]
    public MessagePanel notice;
    [Tooltip("결과 화면 그림. 비어 있으면 글만 나온다 (메뉴 NUMB > Add Missing Objects로 추가)")]
    public ResultArt resultArt;
    public TrayStack approveStack, rejectStack;
    public TerminalPanel terminal;
    public RestorePanel restore;
    public CutscenePlayer cutscene;
    public StampMotion stampMotion;
    public Text feedback;
    [Tooltip("테스트용: 이 스테이지부터 시작 (이전 스테이지 빚은 갚은 것으로 침). M10에서 1로 고정")]
    public int startStage = 1;

    List<TransactionTemplate> templates;
    List<NameEntry> names;
    List<StageInfo> stages;
    Dictionary<string, string> phrases;
    Dictionary<string, MemorySet> memorySets;
    Dictionary<string, List<CutLine>> cutscenes;
    readonly Dictionary<int, string> lastUsed = new Dictionary<int, string>(); // 스테이지*100+슬롯 → 직전 템플릿 ID
    readonly System.Random rng = new System.Random();

    int stage = 1, remainingDebt, earned, cursor;
    List<Transaction> queue;
    readonly List<string> missed = new List<string>();
    Transaction current;

    // 야근 (기획서 6장): 미달이면 모자란 금액 + 이자 20%를 최대 3건으로 번다. 스테이지마다(재도전마다) 한 번
    const int OvertimeMax = 3;
    int lost; // 놓친 금액 (처리한 거래의 수익 - 받은 금액)
    int dayEarned, overtimeTarget; // overtimeTarget > 0 = 야근 중 (earned는 야근에서 번 금액)
    bool overtimeUsed;
    bool InOvertime => overtimeTarget > 0;

    // 튜토리얼 서류를 이미 낸 스테이지. 그 스테이지를 처음 할 때만 튜토리얼 슬롯을 튜토리얼 서류로 고정
    readonly HashSet<int> tutorialShown = new HashSet<int>();

    StageInfo Info => stages.Find(s => s.Stage == stage);

    // 튜토리얼 안내(TutorialGuide)가 읽는 상태
    public Transaction Current => current;
    public bool Busy => directive.panel.activeSelf || result.panel.activeSelf || (notice != null && notice.panel.activeSelf) || (cutscene != null && cutscene.IsPlaying);

    void Start()
    {
        Highlighter.Attach(directive.body); // 형광펜은 업무 지시문에만
        templates = GameData.LoadTemplates();
        names = GameData.LoadNames();
        stages = GameData.LoadStages();
        phrases = GameData.LoadPhrases();
        memorySets = GameData.LoadMemorySets(templates);
        stage = startStage;
        remainingDebt = stages.Where(s => s.Stage >= stage).Sum(s => s.Debt);
        cutscenes = GameData.LoadCutscenes();
        // 인트로는 1일차부터 시작할 때만 (테스트로 startStage를 올리면 건너뜀)
        if (stage == 1) PlayCutscene("Intro", StartStage, fromGame: false); // 게임을 켜자마자라 검은 화면에서 바로
        else StartStage();
    }

    // 컷씬이 없거나 대사가 비어 있으면 바로 다음으로 넘어간다
    // fromGame: 게임 화면에서 검게 페이드한 뒤 시작 / onCovered: 화면이 다 검어졌을 때
    void PlayCutscene(string id, System.Action next, bool fromGame = true, System.Action onCovered = null, System.Action onEnding = null)
    {
        cutscenes.TryGetValue(id, out var lines);
        if (cutscene == null) { onCovered?.Invoke(); onEnding?.Invoke(); next?.Invoke(); }
        else cutscene.Play(lines, next, fromGame, onCovered, onEnding);
    }

    // 그 스테이지 전용 컷씬(Clear.2 등)이 있으면 그것, 없으면 공통(Clear)
    string CutId(string kind) => cutscenes.ContainsKey($"{kind}.{stage}") ? $"{kind}.{stage}" : kind;

    // 마지막 서류를 넣으면: 검게 페이드 → 통과·야근·실패 컷씬 → 검게 → 책상을 거치지 않고 바로 결과 화면 (대사가 없으면 페이드 전환만)
    void ShowAfterCutscene(string kind, DayReport report, string button, System.Action action, bool hideOnPress = true) =>
        PlayCutscene(CutId(kind), () => { if (resultArt != null) resultArt.PlayStamp(); }, onEnding: () =>
        {
            if (resultArt != null) resultArt.Set(report);
            result.Show(resultArt != null ? report.Note : report.Plain, button, action, hideOnPress);
        });

    // [다음 날]: 결과 화면을 열어 둔 채 검게 페이드 → 다 덮이면 결과 화면을 닫고 그날 서류함을 비운다 → 다음 날
    void NextDay(System.Action next) =>
        PlayCutscene("", next, true, () => { result.Hide(); approveStack.Clear(); rejectStack.Clear(); });

    // ── 스테이지 ──────────────────────────────

    void StartStage()
    {
        approveStack.Clear();
        rejectStack.Clear();
        earned = lost = dayEarned = 0;
        overtimeTarget = 0;
        overtimeUsed = false;
        missed.Clear();
        BeginQueue(BuildQueue(), "업무 시작", "서류 더미를 클릭해 첫 거래를 확인하세요.");
    }

    void BeginQueue(List<Transaction> list, string button, string hint)
    {
        queue = list;
        cursor = 0;
        current = queue[0];
        view.Rules = Enumerable.Range(1, stage).Select(i => Phrase($"Rule.{i}").Replace("{", "").Replace("}", "")).ToList();
        findings.Clear();
        Say("");
        directive.Show(DirectiveText(), button, () => Say(hint));
    }

    // 미달 결과 화면의 [야근 시작] → 야근 컷씬 → 야근 지시문 → 거래 최대 3건 (서류함은 그대로 쌓임)
    void StartOvertime(List<TransactionTemplate> pool, int target)
    {
        dayEarned = earned;
        earned = lost = 0;
        overtimeTarget = target;
        BeginQueue(MakeTransactions(pool.OrderBy(_ => rng.Next()).Take(OvertimeMax)), "야근 시작", "서류 더미를 클릭해 야근 거래를 확인하세요.");
    }

    // 슬롯마다 템플릿 하나 (직전 템플릿 제외) + 이름·번호 치환
    List<Transaction> BuildQueue()
    {
        var picked = new List<TransactionTemplate>();
        // 튜토리얼 서류는 그 스테이지를 처음 할 때만 정규 거래 앞에 따로 붙는다 (재도전·다시 하기에서는 없음)
        if (tutorialShown.Add(stage)) picked.AddRange(templates.Where(t => t.Stage == stage && t.Tutorial).OrderBy(t => t.Slot));
        foreach (var slot in templates.Where(t => t.Stage == stage && !t.Tutorial).GroupBy(t => t.Slot).OrderBy(g => g.Key))
        {
            var options = slot.ToList();
            if (options.Count > 1 && lastUsed.TryGetValue(stage * 100 + slot.Key, out var last)) options.RemoveAll(t => t.Id == last);
            var choice = options[rng.Next(options.Count)];
            lastUsed[stage * 100 + slot.Key] = choice.Id;
            picked.Add(choice);
        }
        return MakeTransactions(picked);
    }

    // 템플릿에 이름·번호를 끼워 거래로 만든다
    List<Transaction> MakeTransactions(IEnumerable<TransactionTemplate> picked)
    {
        var list = new List<Transaction>();
        var usedNames = new HashSet<string>();
        var usedNumbers = new HashSet<string>();
        foreach (var t in picked)
        {
            bool needSimilar = t.Errors.Exists(e => e.Kind == "NameMismatch" || e.Kind == "OwnerMismatch");
            var pool = names.Where(n => !usedNames.Contains(n.Name) && (!needSimilar || n.Similar.Length > 0)).ToList();
            var entry = pool[rng.Next(pool.Count)];
            usedNames.Add(entry.Name);

            var number = Variation.RandomNumber(rng, usedNumbers);
            list.Add(new Transaction(t)
            {
                Name = entry.Name,
                Similar = entry.Similar,
                Number = number,
                Memory = t.Type == "Restore" && memorySets.TryGetValue(t.MemorySetId, out var set) ? set : null,
                CardOrder = ShuffledCards(),
            });
        }
        return list;
    }

    // 카드 3장을 섞되, 정답 순서(0,1,2) 그대로는 보여 주지 않는다
    int[] ShuffledCards()
    {
        int[] order;
        do order = new[] { 0, 1, 2 }.OrderBy(_ => rng.Next()).ToArray();
        while (order[0] == 0 && order[1] == 1);
        return order;
    }

    // 컷씬_대사.txt의 대사 (튜토리얼의 오틸리아 안내가 읽음). 없으면 null
    public List<CutLine> Lines(string id) => cutscenes != null && cutscenes.TryGetValue(id, out var lines) ? lines : null;

    string DirectiveText() => Emphasis.Rich(RawDirective(), onDark: false);

    string RawDirective()
    {
        var rules = Enumerable.Range(1, stage).Select(i => $"{i}. {Phrase($"Rule.{i}")}");
        if (InOvertime)
            return $"<b>야근 지시 — {stage}일차</b>\n\n{Phrase("Directive.OT")}\n\n" +
                   $"야근에서 벌 금액: <b>{overtimeTarget:N0} 기억</b>\n처리할 거래: 최대 {queue.Count}건\n\n" +
                   $"<b>■ 규정</b>\n{string.Join("\n", rules)}\n\n— 책임자 오틸리아";
        int tutorials = queue.Count(t => t.Template.Tutorial);
        string count = tutorials > 0 ? $"{queue.Count - tutorials}건 (튜토리얼 {tutorials}건 별도)" : $"{queue.Count}건";
        return $"<b>업무 지시 — {stage}일차 · {Info.Title}</b>\n\n" +
               $"{Phrase($"Directive.{stage}")}\n\n" +
               $"오늘 갚을 빚: <b>{Info.Debt:N0} 기억</b>\n처리할 거래: {count}\n\n" +
               $"<b>■ 규정</b>\n{string.Join("\n", rules)}\n\n— 책임자 오틸리아";
    }

    void ShowResult()
    {
        int debt = Info.Debt;
        int goal = InOvertime ? overtimeTarget : debt;
        if (earned >= goal)
        {
            remainingDebt -= debt; // 야근으로 채웠어도 그날 빚을 갚은 것으로 본다
            bool hasNext = templates.Exists(t => t.Stage == stage + 1);
            var report = Report(true, InOvertime ? "통과 (야근)" : "통과", InOvertime ? "Result.OTPass" : "Result.Pass",
                InOvertime ? $"낮 업무에서 번 금액 {dayEarned:N0}   ·   야근에서 벌 금액 {overtimeTarget:N0}   ·   남은 채무 {remainingDebt:N0}"
                           : $"그날 갚을 빚 {debt:N0}   ·   초과 실적 {earned - debt:N0}   ·   남은 채무 {remainingDebt:N0}");
            // ponytail: 다음 스테이지 템플릿이 아직 없으면 1일차부터 다시 (M7에서 스테이지 3·4 추가)
            if (hasNext) ShowAfterCutscene("Clear", report, "다음 날", () => NextDay(() => { stage++; StartStage(); }), hideOnPress: false);
            else ShowAfterCutscene("Clear", report, "처음부터 다시 하기", () => NextDay(() => { stage = 1; remainingDebt = stages.Sum(s => s.Debt); StartStage(); }), hideOnPress: false);
        }
        else if (!overtimeUsed && OfferOvertime(debt - earned)) { }
        else
        {
            var kinds = missed.GroupBy(m => m).Select(g => g.Count() > 1 ? $"{g.Key} ×{g.Count()}" : g.Key);
            string money = InOvertime
                ? $"야근에서 벌 금액 {overtimeTarget:N0}   ·   모자란 금액 {overtimeTarget - earned:N0}"
                : $"그날 갚을 빚 {debt:N0}   ·   모자란 금액 {debt - earned:N0}";
            ShowAfterCutscene("Fail", Report(false, InOvertime ? "미달 (야근)" : "미달", "Result.Fail", $"{money}\n놓친 오류: {string.Join(", ", kinds)}"), "재시도", StartStage);
        }
    }

    // 획득 금액은 이번 업무(낮 또는 야근), 최종 획득 금액은 낮 업무까지 합한 금액
    DayReport Report(bool success, string title, string phrase, string note) => new DayReport
    {
        Success = success,
        Headline = $"{stage}일차 {title}",
        Phrase = Phrase(phrase),
        Note = note,
        Earned = earned,
        Lost = lost,
        Total = dayEarned + earned,
    };

    // 미달 결과 화면 (기획서 6장). 남은 템플릿 중 수익이 큰 3건으로도 못 채우면 false → 바로 재도전
    bool OfferOvertime(int shortfall)
    {
        int interest = Scoring.RoundTo10(shortfall * 0.2);
        int target = shortfall + interest;
        var used = new HashSet<string>(queue.Select(t => t.Template.Id));
        // ponytail: 스테이지 4 최종 감사 템플릿 제외는 M7에서 그 템플릿이 생기면 추가
        var pool = templates.Where(t => t.Stage == stage && !t.Tutorial && !used.Contains(t.Id)).ToList();
        if (pool.Select(t => t.Reward).OrderByDescending(r => r).Take(OvertimeMax).Sum() < target) return false;

        overtimeUsed = true;
        var report = Report(false, "미달", "Result.Short",
            $"그날 갚을 빚 {Info.Debt:N0}   ·   모자란 금액 {shortfall:N0}   ·   이자 (20%) {interest:N0}\n<b>야근에서 벌 금액 {target:N0}</b>");
        ShowAfterCutscene("Overtime", report, "야근 시작", () => StartOvertime(pool, target));
        return true;
    }

    // ── 업무 (책상 위 클릭) ──────────────────────────────

    public void OpenDocuments()
    {
        current.Viewed = true;
        view.Show(current);
    }

    public void OpenDirective() => directive.Show(DirectiveText(), "닫기", null);

    public void OpenTerminal()
    {
        if (!current.Template.Lookup) { Say("아직 단말기 권한이 없습니다."); return; }
        terminal.Show(Lookup);
    }

    // 기획서 4-2: 서류에 없는 번호면 다른 사람의 기록이 나온다(알려 주지 않음). 서류 번호인데 미등록이면 발견 목록에 자동 추가
    string Lookup(string number)
    {
        bool onPaper = number == current.Number;
        // 기억 번호는 M-0100~M-9999 (입력칸은 숫자 4자리만 받음). 튜토리얼 서류는 안내 순서가 있어 다시 입력만 시킨다
        if (!onPaper && (current.Template.Tutorial || string.CompareOrdinal(number, "M-0100") < 0)) return "번호를 다시 확인하세요.";

        if (number != current.LookupNumber)
        {
            findings.RemoveRecordFindings(); // 앞 조회 기록으로 기록한 발견은 더 이상 맞지 않음
            current.LookupNumber = number;
            current.WrongRecord = onPaper ? null : OtherRecord(number);
        }
        current.LookedUp = true;
        if (!current.Record.Template.Registered)
        {
            findings.Add(new[] { "Lookup.Unregistered" }, "조회 기록 등록 여부");
            return "등록되지 않은 번호입니다. 발견 목록에 자동으로 기록했습니다.";
        }
        terminal.Close();
        view.Show(current); // 조회 기록 칸이 붙은 서류 화면
        return "";
    }

    // 잘못 입력한 번호의 주인 (현재 거래와 늘 다른 사람). 오늘 다른 거래의 번호면 그 거래의 기록,
    // 아니면 번호로 정한 사람·종류·나이 → 같은 거래에서 같은 번호는 늘 같은 기록. 복원 거래면 다른 기억 세트를 복원하게 된다
    Transaction OtherRecord(string number)
    {
        var rnd = new System.Random(int.Parse(number.Substring(2)));
        T Pick<T>(IEnumerable<T> options) { var list = options.ToList(); return list[rnd.Next(list.Count)]; }

        var t = current.Template;
        var today = queue.Find(q => q.Number == number && q.Template.Lookup && q.Template.Registered);
        return new Transaction(new TransactionTemplate
        {
            Type = t.Type,
            Registered = true,
            RecordKind = today?.Template.RecordKind ?? Pick(templates.Where(x => x.Lookup).Select(x => x.MemoryKind).Distinct().Where(k => k != t.MemoryKind && k != t.RecordKind)),
            RecordAge = today?.Template.RecordAge ?? Pick(Enumerable.Range(6, 54).Where(a => a != t.Age && a != t.RecordAge)),
        })
        {
            Name = today?.Owner ?? Pick(names.Select(n => n.Name).Where(n => n != current.Name && n != current.Similar)),
            // 오늘 나오는 기억 세트는 되도록 피한다 (뒤 거래의 답을 미리 보지 않게)
            Memory = current.IsRestore ? memorySets.Values.OrderBy(m => queue.Exists(q => q.Memory == m)).First(m => m != current.Memory) : null,
            CardOrder = ShuffledCards(),
        };
    }

    // 서류 화면 조회 기록 칸의 [기억 복원] 버튼. 끝나면 복원 결과가 붙은 서류 화면으로 돌아간다
    public void OpenRestore()
    {
        if (current.Record.Memory == null) { Debug.LogError($"[템플릿 {current.Template.Id}] 기억 세트가 없어 복원할 수 없습니다."); return; }
        restore.Show(current.Record, () => view.Show(current));
    }

    // 기획서 4-2·4-3: 조회 대상 거래는 조회해야, 복원 거래는 배열에 성공하거나 3회 실패해야 도장을 찍을 수 있다
    bool WorkPending()
    {
        string missing =
            current.Template.Lookup && !current.LookedUp ? "기억 조회가 필요합니다." :
            current.IsRestore && !current.Record.RestoreDone ? "기억 복원이 필요합니다." :
            null;
        if (missing == null) return false;
        (notice != null ? notice : directive).Show($"<b>{missing}</b>", "확인", null); // 열려 있는 동안 Busy라 클릭 막힘
        return true;
    }

    public void StampApprove()
    {
        if (WorkPending()) return;
        Stamped(true, () =>
        {
            current.Stamp = Stamp.Approve;
            current.Reasons.Clear();
            Say("승인 도장을 찍었습니다. 서류함에 넣으세요.");
        });
    }

    public void StampReject()
    {
        if (WorkPending()) return;
        reasonDialog.Show(findings.Findings, reasons => Stamped(false, () => // 사유 [확인] 뒤에 찍는 연출
        {
            current.Stamp = Stamp.Reject;
            current.Reasons = reasons;
            foreach (var r in reasons) r.Misread = current.WrongRecord != null && r.UsesRecord;
            Say($"반려 도장을 찍었습니다 (사유 {reasons.Count}개). 서류함에 넣으세요.");
        }));
    }

    // 도장 연출이 끝난 뒤 판정에 반영 (연출이 없으면 바로)
    void Stamped(bool approve, System.Action apply)
    {
        if (stampMotion != null) stampMotion.Stamp(approve, apply);
        else apply();
    }

    public void FileApprove() => File(Stamp.Approve);
    public void FileReject() => File(Stamp.Reject);

    void File(Stamp tray)
    {
        if (current.Stamp == Stamp.None) { Say("도장을 먼저 찍으세요."); return; }
        if (WorkPending()) return; // 도장을 찍은 뒤 다른 번호로 다시 조회한 복원 거래
        var stack = tray == Stamp.Approve ? approveStack : rejectStack;
        if (stampMotion != null) stampMotion.File(stack, () => Filed(tray)); // 도장 찍힌 종이가 서류함으로 날아가 쌓인 뒤
        else { stack.Add(); Filed(tray); }
    }

    void Filed(Stamp tray)
    {
        // 금액은 결과 화면에서만 공개 (기획서 3장). 튜토리얼 서류는 정산에서 뺀다
        if (!current.Template.Tutorial)
        {
            int gain = Scoring.Calculate(current, tray);
            earned += gain;
            lost += current.Template.Reward - gain;
            missed.AddRange(Scoring.Missed(current, tray));
        }

        cursor++;
        bool overtimeDone = InOvertime && earned >= overtimeTarget; // 야근 금액을 다 벌면 그 자리에서 끝
        if (cursor < queue.Count && !overtimeDone)
        {
            current = queue[cursor];
            findings.Clear();
            Say($"{(tray == Stamp.Approve ? "승인함" : "반려함")}에 넣었습니다. 남은 거래 {queue.Count - cursor}건");
        }
        else
        {
            Say("");
            ShowResult();
        }
    }

    string Phrase(string key) => phrases.TryGetValue(key, out var v) ? v : $"[{key}]";

    void Say(string msg)
    {
        if (feedback != null) feedback.text = msg;
    }
}
