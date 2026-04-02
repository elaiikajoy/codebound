using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;

class Program {
    static bool EvaluateCondition(string cond, Dictionary<string, string> varMap) {
        var match = Regex.Match(cond, @"(?<left>[A-Za-z0-9_]+)\s*(?<op>>|<|>=|<=|==|!=)\s*(?<right>[A-Za-z0-9_.]+)");
        if (!match.Success) { Console.WriteLine("Regex failed"); return false; }

        string leftStr = match.Groups["left"].Value;
        string op = match.Groups["op"].Value;
        string rightStr = match.Groups["right"].Value;

        double leftVal = 0, rightVal = 0;
        bool leftIsNum = double.TryParse(varMap.ContainsKey(leftStr) ? varMap[leftStr] : leftStr, out leftVal);
        bool rightIsNum = double.TryParse(varMap.ContainsKey(rightStr) ? varMap[rightStr] : rightStr, out rightVal);

        if (leftIsNum && rightIsNum) {
            Console.WriteLine($"Math comparing {leftVal} {op} {rightVal}");
            if (op == ">") return leftVal > rightVal;
            if (op == "<") return leftVal < rightVal;
            if (op == ">=") return leftVal >= rightVal;
            if (op == "<=") return leftVal <= rightVal;
            if (op == "==") return Math.Abs(leftVal - rightVal) < 0.0001;
            if (op == "!=") return Math.Abs(leftVal - rightVal) >= 0.0001;
        }
        Console.WriteLine($"String comparing {leftStr} {op} {rightStr}");
        return false;
    }

    static void Main() {
        var map = new Dictionary<string, string> { { "num", "10" } };
        Console.WriteLine(EvaluateCondition("num >= 0", map));
        
        map = new Dictionary<string, string> { { "num", "-5" } };
        Console.WriteLine(EvaluateCondition("num >= 0", map));
    }
}
