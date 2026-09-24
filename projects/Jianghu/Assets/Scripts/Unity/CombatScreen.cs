using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Jianghu.Core.Combat;
using Jianghu.Core.Martial;
using Jianghu.Core.Martial.Display;
using Jianghu.Core.Martial.Morphemes;
using UnityEngine;
using UnityEngine.UI;

namespace Jianghu.Unity
{
    /// <summary>
    /// **전투 화면** — 설계 `docs/ui-combat-view-plan.md`.
    ///
    /// ⚠⚠ **목적은 하나다 — 범위 대가 `T` 를 확정할 수 있게 하는 것.** 예쁜 전투가 아니다.
    ///   그래서 애니메이션·스프라이트·체력바·실시간 재생을 **일부러 만들지 않았다**(설계 §7).
    ///   자동 전투라 결정이 전투 **전에** 전부 들어가므로(작업공간 §8-3) 기다릴 이유도 없다.
    ///
    /// ⚠⚠ **슬롯 하나가 무공을 여럿 담는다**(설계 §1-1). 하나만 담게 하면 자동 측정과 **같은
    ///   맹점**(후보 1개라 `SelectArt` 가 안 돌아간다)을 재현하고, 그러면 여기서 정한 T 도
    ///   실전값이 아니다.
    ///
    /// ⚠ 계산은 전부 Core 다 — <see cref="CombatantBuilder"/> · <see cref="TeamBattleRunner"/>.
    ///   여기는 편성을 받아 넘기고 결과를 그린다.
    /// </summary>
    internal sealed class CombatScreen : MonoBehaviour
    {
        private const int TeamSize = 4;
        private const int SlotCount = TeamSize * 2;

        // ⚠⚠ **`MaxArtsPerSlot = 3` 은 없앴다** (2026-09-24). 상한을 개수로 두면 *"아무 무공이나
        //   3개까지"* 가 되어 **공격 무공 3개**가 허용되는데, 그 편성이 2026-08-23 에 엔진 결함
        //   셋을 드러냈다. 지금 상한은 개수가 아니라 **종류**이고(<see cref="KindTaken"/>),
        //   그 수는 `ArtKind` 가 정한다 — 상수로 또 적으면 두 곳이 갈라진다.

        /// <summary>
        /// 반복 판수. ⚠⚠ **100 이 아니라 1000 이다.** 실측으로 100판의 1σ 가 약 ±5%p 라
        /// `41% vs 43%` 를 못 가른다(설계 §6-1). 1000판의 1σ 는 약 ±1.6%p 다.
        /// ⚠ 실행 시간은 **Unity 안에서 약 600ms** 다(2026-08-23 Play 실측). dotnet 에서 잰
        ///   250ms 의 2.4배인데, 런타임이 다르기 때문이다 — **다른 런타임의 측정치를 그대로 옮기지 않는다.**
        /// </summary>
        private const int ManyFights = 1000;

        private const float PanelWidth = 420f;

        /// <summary>측정 관례를 따른다 — 3·6·10성만 연다(설계 §11-4).</summary>
        private static readonly int[] StageOptions = { 3, 6, 10 };

        private sealed class Slot
        {
            public BattleRow Row;
            public readonly List<MartialArt> Arts = new List<MartialArt>();
        }

        private readonly Slot[] slots = new Slot[SlotCount];
        private readonly List<Button> slotButtons = new List<Button>();
        private readonly List<Button> stageButtons = new List<Button>();

        private int selectedSlot;
        private int stage = 10;

        private Transform rosterContent;
        private Transform outputContent;

        /// <summary>
        /// 위쪽 띠의 알림 자리.
        /// ⚠⚠ **알림을 결과 패널에 쓰지 않는다** (2026-08-23 사용자가 밟음). 처음엔 거기에 썼는데,
        ///   그러면 *"이미 들고 있다"* 같은 사소한 알림 하나가 **방금 1000회 돌린 결과를 지운다.**
        ///   결과는 편성을 바꿔 가며 비교하는 물건이라 함부로 지우면 안 된다.
        /// </summary>
        private Text statusLabel;

        // ─────────────────────────── 조립 ───────────────────────────

        public static CombatScreen Build(Transform parent)
        {
            GameObject root = UiFactory.Panel(parent, "CombatScreen", UiFactory.Background);
            UiFactory.Stretch(root);

            var screen = root.AddComponent<CombatScreen>();
            screen.Compose(root.transform);
            return screen;
        }

        private void Compose(Transform root)
        {
            for (int i = 0; i < SlotCount; i++)
            {
                slots[i] = new Slot { Row = (i % TeamSize) < 2 ? BattleRow.Front : BattleRow.Rear };
            }
            LoadDefaultRoster();

            BuildTopBar(root);

            // ── 우: 결과·로그 (먼저 만든다 — 아래 부품들이 곧바로 여기에 쓴다) ──
            GameObject output = UiFactory.Panel(root, "Output", UiFactory.PanelFill);
            var outRect = output.GetComponent<RectTransform>();
            outRect.anchorMin = Vector2.zero;
            outRect.anchorMax = Vector2.one;
            outRect.offsetMin = new Vector2(16 + MartialPicker.PreferredWidth + 12 + PanelWidth + 12, 16);
            outRect.offsetMax = new Vector2(-16, -56);

            ScrollRect outScroll;
            outputContent = UiFactory.ScrollArea(output.transform, "Output", out outScroll);
            UiFactory.Stretch(outScroll.gameObject, 4f);

            // ── 중: 편성 ──
            GameObject roster = UiFactory.Panel(root, "Roster", UiFactory.PanelFill);
            var rosterRect = roster.GetComponent<RectTransform>();
            rosterRect.anchorMin = new Vector2(0, 0);
            rosterRect.anchorMax = new Vector2(0, 1);
            rosterRect.pivot = new Vector2(0, 0.5f);
            rosterRect.offsetMin = new Vector2(16 + MartialPicker.PreferredWidth + 12, 16);
            rosterRect.offsetMax = new Vector2(16 + MartialPicker.PreferredWidth + 12 + PanelWidth, -56);

            ScrollRect rosterScroll;
            rosterContent = UiFactory.ScrollArea(roster.transform, "Roster", out rosterScroll);
            UiFactory.Stretch(rosterScroll.gameObject, 4f);

            // ── 좌: 무공 고르기 ──
            GameObject left = UiFactory.Panel(root, "Left", UiFactory.PanelFill);
            var leftRect = left.GetComponent<RectTransform>();
            leftRect.anchorMin = new Vector2(0, 0);
            leftRect.anchorMax = new Vector2(0, 1);
            leftRect.pivot = new Vector2(0, 0.5f);
            leftRect.offsetMin = new Vector2(16, 16);
            leftRect.offsetMax = new Vector2(16 + MartialPicker.PreferredWidth, -56);

            // ⚠ 강조를 끈다 — 여기서 누르는 것은 *"보는 것"* 이 아니라 *"슬롯에 담는 것"* 이라,
            //   마지막으로 누른 줄을 강조하면 담긴 것과 헷갈린다.
            new MartialPicker(left, AddToSelectedSlot, highlightSelection: false);

            RefreshRoster();
            ShowMessage("슬롯을 누르고 왼쪽에서 무공을 고른다.");
            AddLine("편성을 고르고 [1회] 나 [" + ManyFights + "회] 를 누른다.", UiFactory.InkDim, 16);
        }

        /// <summary>
        /// 처음 편성 — 설계 §9-5 편성 표의 **1번 칸(대조군)** 이다. 양쪽 다 범위 없는 같은 무공.
        /// ⚠ 화면을 열자마자 무언가 돌려볼 수 있어야 한다. 빈 편성으로 시작하면 여덟 칸을
        ///   채우기 전까지 아무것도 확인할 수 없다.
        /// </summary>
        private void LoadDefaultRoster()
        {
            MartialArt control = FindArt("제화정참");
            if (control == null) return;

            for (int i = 0; i < SlotCount; i++) slots[i].Arts.Add(control);
        }

        private static MartialArt FindArt(string name)
        {
            foreach (MartialArt a in MartialArtCatalog.All) if (a.Name == name) return a;
            return null;
        }

        // ─────────────────────────── 위쪽 띠 ───────────────────────────

        private void BuildTopBar(Transform root)
        {
            GameObject bar = new GameObject("TopBar", typeof(RectTransform));
            bar.transform.SetParent(root, false);

            var rect = bar.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(16, -50);
            rect.offsetMax = new Vector2(-16, -14);

            var layout = bar.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            Text title = UiFactory.Label(bar.transform, "Title", "전투 — 4대4", 22,
                UiFactory.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            LayoutHelp.Width(title.gameObject, 130);

            Text stageLabel = UiFactory.Label(bar.transform, "StageLabel", "경지", 15,
                UiFactory.InkDim, TextAnchor.MiddleLeft);
            LayoutHelp.Width(stageLabel.gameObject, 34);

            for (int i = 0; i < StageOptions.Length; i++)
            {
                int value = StageOptions[i];
                Button b = UiFactory.Row(bar.transform, "Stage" + value, value + "성", 14,
                    UiFactory.RowFill, UiFactory.Ink, () => { stage = value; PaintStage(); RefreshRoster(); }, 30f);
                LayoutHelp.Width(b.gameObject, 52);
                b.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleCenter;
                stageButtons.Add(b);
            }
            PaintStage();

            AddRunButton(bar.transform, "1회", () => RunOnce());
            AddRunButton(bar.transform, ManyFights + "회", () => RunMany());

            statusLabel = UiFactory.Label(bar.transform, "Status", "", 14,
                UiFactory.InkDim, TextAnchor.MiddleLeft);

            // ⚠ 띠 높이가 36px 라 두 줄이 되면 넘친다. 줄바꿈을 끄고 남는 폭을 먹게 둔다.
            statusLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            LayoutHelp.Flexible(statusLabel.gameObject);
        }

        private void AddRunButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            Button b = UiFactory.Row(parent, "Run" + label, label, 15,
                UiFactory.RowSelected, UiFactory.Ink, onClick, 30f);
            LayoutHelp.Width(b.gameObject, 20 + label.Length * 15);
            b.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleCenter;
        }

        private void PaintStage()
        {
            for (int i = 0; i < stageButtons.Count; i++)
            {
                stageButtons[i].GetComponent<Image>().color =
                    StageOptions[i] == stage ? UiFactory.RowSelected : UiFactory.RowFill;
            }
        }

        // ─────────────────────────── 편성판 ───────────────────────────

        private void RefreshRoster()
        {
            UiFactory.Clear(rosterContent);
            slotButtons.Clear();

            for (int i = 0; i < SlotCount; i++)
            {
                if (i % TeamSize == 0)
                {
                    Text head = UiFactory.Label(rosterContent, "Team", i == 0 ? "A팀" : "B팀", 18,
                        UiFactory.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
                    LayoutHelp.Height(head.gameObject, 26);
                }
                AddSlotRows(i);
            }
        }

        private void AddSlotRows(int index)
        {
            Slot slot = slots[index];
            string id = SlotId(index);

            // ── 머리 줄 : 자리 이름 · 열 · 비우기 ──
            GameObject head = UiFactory.HorizontalStrip(rosterContent, "S_" + id, 26f, 6f);

            int captured = index;
            Button pick = UiFactory.Row(head.transform, "Pick", id, 15,
                index == selectedSlot ? UiFactory.RowSelected : UiFactory.RowFill,
                UiFactory.Ink, () => { selectedSlot = captured; RefreshRoster(); }, 26f);
            LayoutHelp.Width(pick.gameObject, 46);
            pick.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleCenter;
            slotButtons.Add(pick);

            Button row = UiFactory.Row(head.transform, "Row", KoreanNames.Of(slot.Row), 14,
                UiFactory.RowFill, UiFactory.Ink,
                () => { slot.Row = BattleRowRule.Opposite(slot.Row); RefreshRoster(); }, 26f);
            LayoutHelp.Width(row.gameObject, 54);
            row.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleCenter;

            Button clear = UiFactory.Row(head.transform, "Clear", "비우기", 13,
                UiFactory.RowFill, UiFactory.InkDim,
                () => { slot.Arts.Clear(); RefreshRoster(); }, 26f);
            LayoutHelp.Width(clear.gameObject, 60);
            clear.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleCenter;

            Text count = UiFactory.Label(head.transform, "Count",
                SlotFill(slot), 13, UiFactory.InkDim, TextAnchor.MiddleLeft);
            LayoutHelp.Flexible(count.gameObject);

            // ── 무공 줄 : 누르면 빠진다 ──
            GameObject artsRow = UiFactory.HorizontalStrip(rosterContent, "A_" + id, 26f, 4f);

            // ⚠ 들여쓴다. 안 그러면 무공 줄이 **위 슬롯 것인지 아래 것인지** 안 보인다 —
            //   1차 Play 에서 `A1 / 제화정참 / A2 / 제화정참` 이 세로로 붙어 그렇게 읽혔다.
            var indent = new GameObject("Indent", typeof(RectTransform));
            indent.transform.SetParent(artsRow.transform, false);
            LayoutHelp.Width(indent, 22);

            if (slot.Arts.Count == 0)
            {
                Text empty = UiFactory.Label(artsRow.transform, "Empty", "(빈자리 — 전투에 안 나간다)", 13,
                    UiFactory.InkDim, TextAnchor.MiddleLeft);
                LayoutHelp.LockWidth(empty, "(빈자리 — 전투에 안 나간다)");
                LayoutHelp.Flexible(empty.gameObject);
                return;
            }

            for (int i = 0; i < slot.Arts.Count; i++)
            {
                MartialArt art = slot.Arts[i];
                bool scoped = art.Scope != AttackScope.Single;

                // ⚠ 범위 무공에 표를 단다 — 이 화면이 답하려는 질문 ①이 *"범위를 몇 개 넣었나"* 다.
                string label = (scoped ? "◈ " : "") + art.Name;
                Button b = UiFactory.Row(artsRow.transform, "Art" + i, label, 14,
                    UiFactory.RowFill, scoped ? UiFactory.Gain : UiFactory.Ink,
                    () => { slot.Arts.Remove(art); RefreshRoster(); }, 26f);
                LayoutHelp.Width(b.gameObject, 16 + label.Length * 14);
            }

            var spacer = new GameObject("Spacer", typeof(RectTransform));
            spacer.transform.SetParent(artsRow.transform, false);
            LayoutHelp.Flexible(spacer);
        }

        private static string SlotId(int index)
        {
            return (index < TeamSize ? "A" : "B") + (index % TeamSize + 1);
        }

        /// <summary>
        /// 슬롯이 **어느 종류를 채웠는지**. 비었으면 `빈 슬롯`.
        ///
        /// ⚠⚠ **그전에는 `1/3` 이었다** (2026-09-24 Play 에서 사용자가 볼 수 있게 된 뒤 교정).
        ///   공격이 찬 슬롯도 `1/3` 이라 *"두 개 더 넣을 수 있다"* 로 읽혔는데, 실제로 넣을 수
        ///   있는 것은 **내공·경공뿐**이었다. 거짓은 아니지만 정확하지도 않았다 —
        ///   `../../CLAUDE.md` §4 지뢰의 *"표시용 수치가 거짓말한다"* 와 같은 계열이다.
        ///   ⚠ 상수 주석에 *"이 상수만 보고 판단하지 말 것"* 이라 적어 뒀는데 **화면에는 그 상수가
        ///     그대로 나가고 있었다.** 주석은 코드를 읽는 사람을 지키지 화면을 지키지 않는다.
        ///
        /// ⚠ 종류 이름을 그대로 적는다 — 기호·약어를 쓰면 **범례가 필요해진다.**
        ///   `supportRichText` 가 꺼져 있어(<see cref="UiFactory"/>) 색으로 구분할 수도 없다.
        /// ⚠ **꽂은 순서가 아니라 공격→내공→경공 고정 순서**로 적는다. 순서가 흔들리면
        ///   같은 편성이 다르게 보인다.
        /// </summary>
        private static string SlotFill(Slot slot)
        {
            if (slot.Arts.Count == 0) return "빈 슬롯";

            var sb = new StringBuilder();
            AppendKind(sb, slot, ArtKind.Attack);
            AppendKind(sb, slot, ArtKind.Internal);
            AppendKind(sb, slot, ArtKind.Movement);
            return sb.ToString();
        }

        private static void AppendKind(StringBuilder sb, Slot slot, ArtKind kind)
        {
            for (int i = 0; i < slot.Arts.Count; i++)
            {
                if (slot.Arts[i].Discipline.KindOf() != kind) continue;

                if (sb.Length > 0) sb.Append('·');
                sb.Append(kind.ToKorean());
                return;
            }
        }

        /// <summary>
        /// <paramref name="art"/> 와 **같은 종류**로 이미 슬롯에 들어 있는 무공. 없으면 null.
        /// 정의서 §0-1 — 공격·내공·경공 각 최대 1개다.
        /// </summary>
        private static MartialArt KindTaken(Slot slot, MartialArt art)
        {
            ArtKind kind = art.Discipline.KindOf();
            for (int i = 0; i < slot.Arts.Count; i++)
            {
                if (slot.Arts[i].Discipline.KindOf() == kind) return slot.Arts[i];
            }
            return null;
        }

        private void AddToSelectedSlot(MartialArt art)
        {
            if (art == null) return;

            Slot slot = slots[selectedSlot];

            // ⚠⚠ **종류당 하나다** (정의서 §0-1 장착 규정). 개수가 아니라 종류로 막는다 —
            //   `CombatantBuilder` 가 어차피 거부하므로 여기서 안 막으면 **Play 중에 예외로 터진다.**
            //   막는 김에 *무엇이 이미 그 자리에 있는지*까지 말한다. 안 그러면 사용자가
            //   왜 안 들어가는지 알 수 없다(§6 — 찾는 비용을 내가 진다).
            MartialArt taken = KindTaken(slot, art);
            if (taken != null)
            {
                ShowMessage(
                    SlotId(selectedSlot) + " 의 " + art.Discipline.KindOf().ToKorean() +
                    " 자리엔 이미 " + taken.Name + " 이(가) 있다 — 그것을 눌러 빼고 넣는다.");
                return;
            }
            if (slot.Arts.Contains(art))
            {
                // ⚠ 같은 무공을 두 번 배울 수는 없다. **막는 것이 맞다** — 다만 무엇을 하면 되는지까지 말한다.
                ShowMessage(SlotId(selectedSlot) + " 는 이미 " + art.Name + " 을(를) 들고 있다.");
                return;
            }

            slot.Arts.Add(art);
            RefreshRoster();
        }

        // ─────────────────────────── 전투 ───────────────────────────

        /// <summary>
        /// 편성을 엔진이 받는 형태로 옮긴다. 빈 슬롯은 빠진다.
        /// ⚠⚠ **자리 이름을 붙인다**(`A1 제화정참`). 로그가 이름 문자열로만 사람을 가리키므로
        ///   (`CombatLogEntry`), 안 붙이면 *"제화정참의 제화정참 → 제화정참"* 이 된다.
        /// </summary>
        private List<BattlePlacement> BuildTeam(int from)
        {
            var team = new List<BattlePlacement>(TeamSize);
            for (int i = from; i < from + TeamSize; i++)
            {
                Slot slot = slots[i];
                if (slot.Arts.Count == 0) continue;

                Combatant c = CombatantBuilder.Build(
                    SlotId(i) + " " + slot.Arts[0].Name, slot.Arts, stage);
                team.Add(new BattlePlacement(c, slot.Row));
            }
            return team;
        }

        private bool TryBuildTeams(out List<BattlePlacement> a, out List<BattlePlacement> b)
        {
            a = BuildTeam(0);
            b = BuildTeam(TeamSize);

            if (a.Count == 0 || b.Count == 0)
            {
                // ⚠ 이것은 알림이 아니라 **돌리지 못한 결과**다. 결과 패널에도 남긴다.
                string why = "양쪽 모두 한 명 이상이어야 싸운다. (A " + a.Count + "명 · B " + b.Count + "명)";
                ShowMessage(why);
                UiFactory.Clear(outputContent);
                AddLine(why, UiFactory.Loss, 16);
                return false;
            }
            return true;
        }

        private void RunOnce()
        {
            List<BattlePlacement> a, b;
            if (!TryBuildTeams(out a, out b)) return;

            ShowMessage("");   // ⚠ 위와 같은 이유.
            TeamCombatResult r = TeamBattleRunner.RunOnce(a, b);

            UiFactory.Clear(outputContent);
            AddHeading("1회 — 시드 " + TeamBattleRunner.FirstSeed);
            AddLine(r.ToString(), UiFactory.Ink, 18);
            AddLine(Composition(), UiFactory.InkDim, 14);

            AddHeading("전투 기록");
            var sb = new StringBuilder();
            for (int i = 0; i < r.Log.Count; i++) sb.Append(r.Log[i]).Append('\n');
            AddLine(sb.ToString(), UiFactory.InkDim, 14);
        }

        private void RunMany()
        {
            List<BattlePlacement> a, b;
            if (!TryBuildTeams(out a, out b)) return;

            ShowMessage("");   // ⚠ 지난 알림을 지운다. 새 결과 옆에 남아 있으면 거짓말이 된다.

            var watch = Stopwatch.StartNew();
            TeamBattleSummary s = TeamBattleRunner.Run(a, b, ManyFights);
            watch.Stop();

            UiFactory.Clear(outputContent);
            AddHeading(ManyFights + "회 — A팀 승률");
            AddLine((s.TeamAWinRate * 100).ToString("F1") + "%  ± "
                    + (s.StandardError * 100).ToString("F1") + "%p", UiFactory.Ink, 30);

            // ⚠⚠ 표준오차를 **말로도 적는다.** 숫자 옆의 `±` 만으로는 그 폭보다 작은 차이를
            //   근거로 삼는 것을 못 막는다 — 그것이 이 화면이 막으려는 실수다(설계 §6-1).
            AddLine("⚠ ±" + (s.StandardError * 100).ToString("F1") + "%p 보다 작은 차이는 차이가 아니다. "
                    + "편성끼리 비교할 때 이 선을 먼저 긋는다.", UiFactory.Loss, 14);

            AddLine("A " + s.TeamAWins + "승 · B " + s.TeamBWins + "승 · 무 " + s.Draws
                    + "   (무승부는 반 점)", UiFactory.InkDim, 15);
            AddLine("평균 " + s.AverageRounds.ToString("F1") + "경합 · 평균 생존 "
                    + s.AverageSurvivorsA.ToString("F2") + "대" + s.AverageSurvivorsB.ToString("F2"),
                    UiFactory.InkDim, 15);
            AddLine("실행 " + watch.ElapsedMilliseconds + "ms", UiFactory.InkDim, 13);

            AddHeading("편성");
            AddLine(Composition(), UiFactory.InkDim, 14);
        }

        /// <summary>
        /// 지금 편성을 글로 적는다. **결과와 편성을 함께 남기지 않으면** 여러 칸을 돌린 뒤
        /// 어느 숫자가 어느 편성이었는지 잃는다 — 설계 §9-5 의 편성 표가 그것을 막으려는 것이다.
        /// </summary>
        private string Composition()
        {
            var sb = new StringBuilder();
            sb.Append("경지 ").Append(stage).Append("성\n");

            for (int i = 0; i < SlotCount; i++)
            {
                if (i % TeamSize == 0) sb.Append(i == 0 ? "A팀\n" : "B팀\n");

                sb.Append("  ").Append(SlotId(i)).Append(' ')
                  .Append(KoreanNames.Of(slots[i].Row)).Append("  ");

                if (slots[i].Arts.Count == 0) sb.Append("(빈자리)");
                for (int j = 0; j < slots[i].Arts.Count; j++)
                {
                    if (j > 0) sb.Append(" · ");
                    MartialArt art = slots[i].Arts[j];
                    if (art.Scope != AttackScope.Single) sb.Append("◈");
                    sb.Append(art.Name);
                }
                sb.Append('\n');
            }

            sb.Append("범위 무공 — A ").Append(ScopeCount(0)).Append("개 · B ").Append(ScopeCount(TeamSize)).Append("개");
            return sb.ToString();
        }

        /// <summary>그 팀이 든 **범위 무공 수**. 질문 ①이 바로 이 값과 승률의 관계다.</summary>
        private int ScopeCount(int from)
        {
            int n = 0;
            for (int i = from; i < from + TeamSize; i++)
            {
                for (int j = 0; j < slots[i].Arts.Count; j++)
                {
                    if (slots[i].Arts[j].Scope != AttackScope.Single) { n++; break; }
                }
            }
            return n;
        }

        // ─────────────────────────── 출력 ───────────────────────────

        /// <summary>
        /// 위쪽 띠에 한 줄 알린다. ⚠ **결과 패널은 건드리지 않는다** — 위 <see cref="statusLabel"/> 주석.
        /// </summary>
        private void ShowMessage(string text)
        {
            if (statusLabel != null) statusLabel.text = text;
        }

        private void AddHeading(string text)
        {
            Text t = UiFactory.Label(outputContent, "H", text, 18, UiFactory.Ink,
                TextAnchor.UpperLeft, FontStyle.Bold);
            LayoutHelp.Height(t.gameObject, 30);
        }

        /// <summary>
        /// 여러 줄 글. ⚠ 여기는 **폭을 잠그지 않는다** — 로그는 길어서 접히는 편이 낫고,
        /// 레이아웃 그룹이 폭을 정해 주므로 표 칸처럼 서로 밀어내지 않는다.
        /// </summary>
        private void AddLine(string text, Color color, int size)
        {
            Text t = UiFactory.Label(outputContent, "L", text, size, color);
            LayoutHelp.Element(t.gameObject).flexibleWidth = 1;
        }
    }
}
