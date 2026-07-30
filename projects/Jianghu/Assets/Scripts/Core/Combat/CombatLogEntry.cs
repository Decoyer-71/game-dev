namespace Jianghu.Core.Combat
{
    /// <summary>전투 로그 한 줄이 무엇을 기록한 것인가.</summary>
    public enum CombatLogKind
    {
        /// <summary>초식을 쓴 행동.</summary>
        Action = 0,

        /// <summary>출혈·중독·기력소실이 턴 시작에 발동한 것.</summary>
        StatusTick = 1,

        /// <summary>마비로 행동하지 못한 것.</summary>
        Incapacitated = 2,
    }

    /// <summary>
    /// 전투 한 사건의 기록.
    ///
    /// ⚠ 이 로그가 **반증 조건 1의 판정 수단**이다(docs/jianghu-design.md §1).
    /// "무공 조합을 바꿔도 차이를 체감할 수 없다" 를 확인하려면, 무엇이 왜 일어났는지
    /// 사람이 읽어서 알 수 있어야 한다. 그래서 수치를 뭉뚱그리지 않고 항목별로 남긴다.
    ///
    /// ⚠⚠ 기력소실이 실제로 체감되려면 **"초식을 못 써서 평타로 내려앉았다"가 로그에 보여야 한다.**
    /// MTG 마나번이 폐지된 이유가 "체감이 안 돼서"였다(docs/martial-system-proposal.md §5-4).
    /// </summary>
    public sealed class CombatLogEntry
    {
        public CombatLogKind Kind { get; }
        public int Turn { get; }
        public string ActorName { get; }
        public string TargetName { get; }
        public string ArtName { get; }

        /// <summary>시도한 타격 횟수(다단 초식은 2 이상).</summary>
        public int AttemptedHits { get; }

        /// <summary>실제로 명중한 횟수. 0 이면 전부 회피당했다.</summary>
        public int LandedHits { get; }

        /// <summary>이 사건으로 준 피해.</summary>
        public int Damage { get; }

        /// <summary>소모하거나 잃은 기력.</summary>
        public int QiSpent { get; }

        /// <summary>대상의 남은 체력.</summary>
        public int TargetHealthAfter { get; }

        /// <summary>부가 설명. 상태이상 부여·발동 등. 없으면 null.</summary>
        public string Note { get; }

        private CombatLogEntry(
            CombatLogKind kind, int turn, string actorName, string targetName, string artName,
            int attemptedHits, int landedHits, int damage, int qiSpent, int targetHealthAfter, string note)
        {
            Kind = kind;
            Turn = turn;
            ActorName = actorName;
            TargetName = targetName;
            ArtName = artName;
            AttemptedHits = attemptedHits;
            LandedHits = landedHits;
            Damage = damage;
            QiSpent = qiSpent;
            TargetHealthAfter = targetHealthAfter;
            Note = note;
        }

        /// <summary>초식을 쓴 행동.</summary>
        public static CombatLogEntry Action(
            int turn, string actorName, string targetName, string artName,
            int attemptedHits, int landedHits, int damage, int qiSpent, int targetHealthAfter, string note = null)
        {
            return new CombatLogEntry(CombatLogKind.Action, turn, actorName, targetName, artName,
                attemptedHits, landedHits, damage, qiSpent, targetHealthAfter, note);
        }

        /// <summary>상태이상이 턴 시작에 발동했다.</summary>
        public static CombatLogEntry StatusTick(
            int turn, string sufferer, string statusName, int damage, int qiLost, int healthAfter)
        {
            return new CombatLogEntry(CombatLogKind.StatusTick, turn, sufferer, sufferer, statusName,
                0, 0, damage, qiLost, healthAfter, null);
        }

        /// <summary>마비 등으로 행동하지 못했다.</summary>
        public static CombatLogEntry Incapacitated(int turn, string actorName, string reason)
        {
            return new CombatLogEntry(CombatLogKind.Incapacitated, turn, actorName, actorName, reason,
                0, 0, 0, 0, 0, null);
        }

        /// <summary>사람이 읽는 한 줄. UI 가 붙기 전까지 이게 유일한 관찰 창구다.</summary>
        public override string ToString()
        {
            string head = "[" + Turn + "턴] ";

            switch (Kind)
            {
                case CombatLogKind.Incapacitated:
                    return head + ActorName + " : " + ArtName + " — 행동 불가";

                case CombatLogKind.StatusTick:
                {
                    string body = head + ActorName + " : " + ArtName;
                    if (Damage > 0) body += " 피해 " + Damage + " (남은 체력 " + TargetHealthAfter + ")";
                    if (QiSpent > 0) body += " 기력 -" + QiSpent;
                    return body;
                }

                default:
                {
                    string body = head + ActorName + "의 " + ArtName;

                    if (LandedHits == 0)
                    {
                        body += " → " + TargetName + " : 회피당함";
                    }
                    else
                    {
                        if (AttemptedHits > 1) body += " " + LandedHits + "/" + AttemptedHits + "타";
                        body += " → " + TargetName + " : 피해 " + Damage + " (남은 체력 " + TargetHealthAfter + ")";
                    }

                    if (QiSpent > 0) body += " (기력 -" + QiSpent + ")";
                    if (!string.IsNullOrEmpty(Note)) body += " " + Note;
                    return body;
                }
            }
        }
    }
}
