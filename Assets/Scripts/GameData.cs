using System.Collections.Generic;
using System.Text;
using UnityEngine;

public struct TemplateError
{
    public string Kind; // GameData.KnownKinds 참고
    public string Doc;  // Application / Contract / Lookup(조회 기록) / Restore(복원 결과)
}

public class TransactionTemplate
{
    public string Id, Type, AppDesc, ConDesc, Consent, MemoryKind, RecordKind, MemorySetId, Answer;
    public int Stage, Slot, Reward, Age, RecordAge; // Age = 신청서, RecordAge = 조회 기록의 기억 저장 시기
    public bool AppSign, ConSign, Registered;
    public bool Lookup; // 단말기 조회 대상 (Registered 칸이 '-'가 아님). 스테이지 1 거래는 조회 없음
    public bool Tutorial; // 그 스테이지를 처음 할 때만 거래 앞에 덧붙는 튜토리얼 서류 (Slot = 튜토리얼끼리 순서, 정산·건수에서 빠짐)
    public List<TemplateError> Errors = new List<TemplateError>();

    public bool HasError(string kind, string doc) => Errors.Exists(e => e.Kind == kind && e.Doc == doc);
}

public class NameEntry
{
    public string Name, Similar; // Similar가 비어 있으면 이름 오류에 쓰지 않는 이름
}

// 기억 복원 카드 세트 (기획서 4-3). Cards는 정답(시간) 순서.
public class MemorySet
{
    public string Id, Result, Clue;
    public string[] Cards;
}

// 컷씬 자막 한 줄 (클릭 한 번)
public class CutLine
{
    public Texture2D Image;
    public string Speaker, Text; // Speaker가 비면 내레이션
}

public class StageInfo
{
    public int Stage, Debt;
    public string Title;
}

// 기획서 7장 값 치환: 이름·기억 번호 뽑기와 번호 자리바꿈
public static class Variation
{
    // M-0100 ~ M-9999. 자리바꿈이 가능하도록 이웃한 두 자리가 다른 번호만 뽑는다.
    public static string RandomNumber(System.Random rng, HashSet<string> used)
    {
        while (true)
        {
            var n = "M-" + rng.Next(100, 10000).ToString("D4");
            if (!used.Contains(n) && SwapCandidates(n).Count > 0) { used.Add(n); return n; }
        }
    }

    // 서로 다른 이웃한 두 자리 중 하나를 골라 바꾼다 (예: M-0712 → M-0721)
    public static string SwapDigits(string number, System.Random rng)
    {
        var candidates = SwapCandidates(number);
        int i = candidates[rng.Next(candidates.Count)];
        var c = number.ToCharArray();
        (c[i], c[i + 1]) = (c[i + 1], c[i]);
        return new string(c);
    }

    static List<int> SwapCandidates(string number)
    {
        var list = new List<int>();
        for (int i = 2; i < number.Length - 1; i++) // "M-" 다음부터
            if (number[i] != number[i + 1]) list.Add(i);
        return list;
    }
}

// Resources/Data의 CSV를 읽는다. 데이터 모순은 불러올 때 에러 로그로 알린다.
public static class GameData
{
    static readonly string[] KnownKinds = { "NameMismatch", "NumberMismatch", "SignMissing", "DescMismatch", "OwnerMismatch", "KindMismatch", "AgeMismatch", "Unregistered", "RestoreMismatch", "ConsentMissing", "UnderTen" };
    static readonly string[] LookupKinds = { "OwnerMismatch", "KindMismatch", "AgeMismatch", "Unregistered", "UnderTen" };

    public static List<TransactionTemplate> LoadTemplates()
    {
        var list = new List<TransactionTemplate>();
        foreach (var r in Load("Data/Templates"))
        {
            var t = new TransactionTemplate
            {
                Id = r["TemplateId"], Stage = Int(r["Stage"]), Slot = Int(r["Slot"]), Type = r["Type"],
                Reward = Int(r["Reward"]), AppDesc = r["AppDesc"], ConDesc = r["ConDesc"],
                AppSign = r["AppSign"] == "Y", ConSign = r["ConSign"] == "Y", Consent = r["Consent"],
                Registered = r["Registered"] == "Y", Lookup = r["Registered"] != "-", MemoryKind = r["MemoryKind"],
                RecordKind = r["RecordKind"].Length > 0 ? r["RecordKind"] : r["MemoryKind"], Age = Int(r["Age"]),
                RecordAge = Int(r["RecordAge"].Length > 0 ? r["RecordAge"] : r["Age"]),
                MemorySetId = r["MemorySetId"], Answer = r["Answer"], Tutorial = r["Tutorial"] == "Y",
            };
            foreach (var e in r["Errors"].Split('|'))
            {
                if (e.Length == 0) continue;
                var p = e.Split('@');
                t.Errors.Add(new TemplateError { Kind = p[0], Doc = p.Length > 1 ? p[1] : "" });
            }
            Validate(t);
            list.Add(t);
        }
        return list;
    }

    public static List<NameEntry> LoadNames()
    {
        var list = new List<NameEntry>();
        foreach (var r in Load("Data/Names"))
            list.Add(new NameEntry { Name = r["Name"], Similar = r["Similar"] });
        return list;
    }

    public static List<StageInfo> LoadStages()
    {
        var list = new List<StageInfo>();
        foreach (var r in Load("Data/Stages"))
            list.Add(new StageInfo { Stage = Int(r["Stage"]), Title = r["Title"], Debt = Int(r["Debt"]) });
        return list;
    }

    // 복원 거래마다 세트가 있는지, 복원 결과와 신청서 설명 차이가 RestoreMismatch와 맞는지 함께 검사
    public static Dictionary<string, MemorySet> LoadMemorySets(List<TransactionTemplate> templates)
    {
        var dict = new Dictionary<string, MemorySet>();
        foreach (var r in Load("Data/MemorySets"))
            dict[r["SetId"]] = new MemorySet { Id = r["SetId"], Cards = new[] { r["Card1"], r["Card2"], r["Card3"] }, Result = r["Result"], Clue = r["Clue"] };

        foreach (var t in templates)
        {
            if (t.Type != "Restore") continue;
            if (!dict.TryGetValue(t.MemorySetId, out var set)) { Debug.LogError($"[템플릿 {t.Id}] 기억 세트 {t.MemorySetId}가 MemorySets.csv에 없음"); continue; }
            if ((t.AppDesc != set.Result) != t.Errors.Exists(e => e.Kind == "RestoreMismatch"))
                Debug.LogError($"[템플릿 {t.Id}] 신청서 기억 설명과 복원 결과({set.Result})의 차이가 RestoreMismatch와 맞지 않음");
        }
        return dict;
    }

    // Resources/Data/컷씬_대사.txt (사람이 쓰는 양식): [컷씬ID] → "이미지: 파일이름" → "이름: 대사".
    // 이미지는 Resources/Cutscenes 아래 어느 폴더에 있든 파일 이름으로 찾는다 (폴더가 달라도 이름은 겹치지 않게).
    public static Dictionary<string, List<CutLine>> LoadCutscenes()
    {
        var result = new Dictionary<string, List<CutLine>>();
        var asset = Resources.Load<TextAsset>("Data/컷씬_대사");
        if (asset == null) { Debug.LogError("컷씬 대사 파일 없음: Resources/Data/컷씬_대사.txt"); return result; }

        var images = new Dictionary<string, Texture2D>();
        foreach (var tex in Resources.LoadAll<Texture2D>("Cutscenes")) images[tex.name] = tex;

        string id = null;
        Texture2D image = null;
        foreach (var raw in asset.text.Split('\n'))
        {
            var line = raw.Trim().TrimStart('﻿');
            if (line.Length == 0 || line[0] == '#' || line[0] == '=') continue;
            if (line[0] == '[')
            {
                int end = line.IndexOf(']');
                id = end > 0 ? line.Substring(1, end - 1).Trim() : null;
                image = null;
                continue;
            }
            int colon = line.IndexOf(':');
            if (id == null || colon < 0) continue;
            string head = line.Substring(0, colon).Trim(), body = line.Substring(colon + 1).Trim();

            if (head == "이미지")
            {
                if (!images.TryGetValue(body, out image)) Debug.LogError($"[컷씬 {id}] 이미지 {body}가 Resources/Cutscenes에 없음");
                continue;
            }
            if (body.Length == 0) continue;
            if (!result.TryGetValue(id, out var list)) result[id] = list = new List<CutLine>();
            list.Add(new CutLine
            {
                Image = image,
                Speaker = head == "내레이션" ? "" : head,
                Text = body.Replace(" / ", "\n").Replace("/", "\n"), // "/" = 자막 안 줄바꿈
            });
        }
        return result;
    }

    public static Dictionary<string, string> LoadPhrases()
    {
        var dict = new Dictionary<string, string>();
        foreach (var r in Load("Data/Phrases")) dict[r["Key"]] = r["Text"];
        return dict;
    }

    static void Validate(TransactionTemplate t)
    {
        void Check(bool ok, string msg) { if (!ok) Debug.LogError($"[템플릿 {t.Id}] {msg}"); }

        foreach (var e in t.Errors)
        {
            Check(System.Array.IndexOf(KnownKinds, e.Kind) >= 0, $"알 수 없는 오류 종류 {e.Kind}");
            string fixedDoc = System.Array.IndexOf(LookupKinds, e.Kind) >= 0 ? "Lookup" : e.Kind == "RestoreMismatch" ? "Restore" : e.Kind == "ConsentMissing" ? "Application" : null;
            Check(fixedDoc != null ? e.Doc == fixedDoc : e.Doc == "Application" || e.Doc == "Contract", $"오류 위치가 잘못됨 {e.Kind}@{e.Doc}");
            Check(fixedDoc == null || t.Lookup, $"{e.Kind}는 조회 대상 거래(Registered가 Y/N)에만 쓸 수 있음");
            Check(e.Kind != "RestoreMismatch" || t.Type == "Restore", "RestoreMismatch는 복원 거래에만 쓸 수 있음");
        }
        Check((t.Answer == "Reject") == (t.Errors.Count > 0), "Answer와 Errors가 맞지 않음");
        Check(t.AppSign != t.HasError("SignMissing", "Application"), "AppSign과 SignMissing@Application이 맞지 않음");
        Check(t.ConSign != t.HasError("SignMissing", "Contract"), "ConSign과 SignMissing@Contract가 맞지 않음");
        Check((t.AppDesc != t.ConDesc) == t.Errors.Exists(e => e.Kind == "DescMismatch"), "기억 설명 차이와 DescMismatch가 맞지 않음");
        if (!t.Lookup) return;
        Check(t.Registered != t.Errors.Exists(e => e.Kind == "Unregistered"), "Registered N과 Unregistered가 맞지 않음");
        Check((t.RecordKind != t.MemoryKind) == t.Errors.Exists(e => e.Kind == "KindMismatch"), "RecordKind 차이와 KindMismatch가 맞지 않음");
        Check((t.RecordAge != t.Age) == t.Errors.Exists(e => e.Kind == "AgeMismatch"), "RecordAge 차이와 AgeMismatch가 맞지 않음");
        // 규정 (기획서 5장): 19세 미만은 동의서 필요(스테이지 3부터), 10세 미만은 동의서가 있어도 불가(스테이지 4부터)
        Check((t.Stage >= 3 && t.Age < 19 && t.Consent != "Y") == t.Errors.Exists(e => e.Kind == "ConsentMissing"), "동의서 없는 미성년자와 ConsentMissing이 맞지 않음");
        Check((t.Stage >= 4 && t.Age < 10) == t.Errors.Exists(e => e.Kind == "UnderTen"), "10세 미만과 UnderTen이 맞지 않음");
        Check(t.Stage < 3 || (t.Age < 19) == (t.RecordAge < 19) && (t.Age < 10) == (t.RecordAge < 10), "신청서와 조회 기록의 기억 저장 시기가 규정 나이(10·19세)의 양쪽으로 갈려 정답이 애매함");
        Check(t.Type != "Restore" || t.MemorySetId != "-", "복원 거래에 MemorySetId가 없음");
    }

    static int Int(string s) => int.TryParse(s, out var v) ? v : -1;

    static List<Dictionary<string, string>> Load(string resource)
    {
        var asset = Resources.Load<TextAsset>(resource);
        if (asset == null) { Debug.LogError($"CSV 없음: Resources/{resource}"); return new List<Dictionary<string, string>>(); }
        var rows = ParseCsv(asset.text);
        var result = new List<Dictionary<string, string>>();
        for (int i = 1; i < rows.Count; i++)
        {
            if (rows[i].Count > rows[0].Count) // 따옴표 없이 쉼표를 쓰면 글이 잘리므로 알림
                Debug.LogError($"CSV {resource} {i + 1}행: 칸이 {rows[i].Count}개로 헤더({rows[0].Count}개)보다 많습니다. 쉼표가 든 칸은 큰따옴표로 감싸세요. 첫 칸: {rows[i][0]}");
            var row = new Dictionary<string, string>();
            for (int c = 0; c < rows[0].Count; c++) row[rows[0][c]] = c < rows[i].Count ? rows[i][c] : "";
            result.Add(row);
        }
        return result;
    }

    // 큰따옴표로 감싼 칸(쉼표·줄바꿈·"" 포함)을 지원하는 최소 CSV 파서
    public static List<List<string>> ParseCsv(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var cell = new StringBuilder();
        bool quoted = false;
        text = text.TrimStart('﻿');

        for (int i = 0; i < text.Length; i++)
        {
            char ch = text[i];
            if (quoted)
            {
                if (ch == '"' && i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                else if (ch == '"') quoted = false;
                else cell.Append(ch);
            }
            else if (ch == '"') quoted = true;
            else if (ch == ',') { row.Add(cell.ToString()); cell.Clear(); }
            else if (ch == '\n')
            {
                row.Add(cell.ToString()); cell.Clear();
                if (row.Count > 1 || row[0].Length > 0) rows.Add(row);
                row = new List<string>();
            }
            else if (ch != '\r') cell.Append(ch);
        }
        row.Add(cell.ToString());
        if (row.Count > 1 || row[0].Length > 0) rows.Add(row);
        return rows;
    }
}
