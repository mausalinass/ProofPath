using System.Text.RegularExpressions;

namespace ProofPath.Domain.Entities;

// Explicit professional statements only. This conservative v1 detector never infers personality
// or grants Strong based on prose, repetition or a provider-supplied label.
public static class BehaviorEvidenceRules
{
    private static readonly Dictionary<string, (string Actions, string Claims)> Rules = new()
    {
        ["COLLABORATION"] = ("collaborated|coordinated|partnered|colaboré|coordiné", "collaboration|teamwork|collaborative|colaboración|trabajo en equipo"),
        ["CROSS_FUNCTIONAL_COLLABORATION"] = ("collaborated with|partnered with|coordinated with|colaboré con", "cross-functional|interdisciplinary|interfuncional"),
        ["COMMUNICATION"] = ("presented|documented|explained|communicated|presenté|documenté|expliqué", "communication|communicator|comunicación"),
        ["STAKEHOLDER_COMMUNICATION"] = ("presented to stakeholders|communicated with stakeholders|presented to clients|presenté a clientes", "stakeholder communication|comunicación con clientes"),
        ["OWNERSHIP"] = ("owned|took responsibility|maintained|operated|asumí|mantuve", "ownership|accountability|responsabilidad"),
        ["LEADERSHIP"] = ("led|directed|managed a team|lideré|dirigí", "leadership|leader|liderazgo|líder"),
        ["INITIATIVE"] = ("initiated|proposed|introduced|inicié|propuse", "initiative|proactive|iniciativa|proactivo"),
        ["ADAPTABILITY"] = ("adapted|transitioned|learned|adapté|aprendí", "adaptability|adaptable|adaptabilidad"),
        ["PROBLEM_SOLVING"] = ("resolved|diagnosed|debugged|investigated|resolví|diagnostiqué|investigué", "problem-solving|problem solving|resolución de problemas"),
        ["MENTORING"] = ("mentored|coached|tutored|trained teammates|mentoré|capacité", "mentoring|mentorship|mentor|mentoría")
    };
    public static string? Strength(string themeKey, string statement)
    {
        if (!Rules.TryGetValue(themeKey, out var rule)) return null;
        bool Matches(string expression) => Regex.IsMatch(statement, @"\b(?:" + expression + @")\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        if (Matches(rule.Actions) && statement.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 2) return "Moderate";
        return Matches(rule.Claims) ? "Weak" : null;
    }
}
