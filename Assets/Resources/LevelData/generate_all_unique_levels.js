const fs = require('fs');
const path = require('path');

const levelDataDir = "c:\\Users\\Elaika Joy Santiago\\CP2\\Assets\\Resources\\LevelData";

// We will generate exactly 99 unique problems programmatically.
let uniqueProblems = [];

// Helper to push a problem
function addProblem(name, desc, obj, code, expected, keys) {
    uniqueProblems.push({ name, desc, obj, code, expected, keys });
}

// Phase 1: Variables (Levels 2 - 11) - 10 levels
for (let i = 2; i <= 11; i++) {
    let val = i * 5;
    addProblem(
        `Variables Pt ${i-1}`,
        "Learn to declare variable integers.",
        `Declare an integer variable named 'score' with value ${val}, then print it.`,
        "public class Solution {\n    public static void main(String[] args) {\n        // Type your answer below:\n        \n    }\n}",
        `${val}`,
        ["int score", `${val}`, "System.out.println(score);"]
    );
}

// Phase 2: String Variables (Levels 12 - 21) - 10 levels
const words = ["Apple", "Orange", "Banana", "Grape", "Mango", "Peach", "Plum", "Berry", "Cherry", "Melon"];
for (let i = 12; i <= 21; i++) {
    let word = words[i - 12];
    addProblem(
        `Strings Pt ${i-11}`,
        "Working with text variables.",
        `Declare a String named 'fruit' with value "${word}", then print it.`,
        "public class Solution {\n    public static void main(String[] args) {\n        // Type your answer below:\n        \n    }\n}",
        `${word}`,
        ["String fruit", `"${word}"`, "System.out.println"]
    );
}

// Phase 3: Addition (Levels 22 - 31) - 10 levels
for (let i = 22; i <= 31; i++) {
    let a = i;
    let b = i * 2;
    let sum = a + b;
    addProblem(
        `Addition Pt ${i-21}`,
        "Adding two numbers.",
        `Declare integers a = ${a} and b = ${b}. Print their sum.`,
        "public class Solution {\n    public static void main(String[] args) {\n        // Type your answer below:\n        \n    }\n}",
        `${sum}`,
        [`a = ${a}`, `b = ${b}`, "System.out.println"]
    );
}

// Phase 4: Subtraction (Levels 32 - 41) - 10 levels
for (let i = 32; i <= 41; i++) {
    let a = i * 3;
    let b = i;
    let diff = a - b;
    addProblem(
        `Subtraction Pt ${i-31}`,
        "Subtracting two numbers.",
        `Declare integers x = ${a} and y = ${b}. Print their difference (x - y).`,
        "public class Solution {\n    public static void main(String[] args) {\n        // Type your answer below:\n        \n    }\n}",
        `${diff}`,
        [`x = ${a}`, `y = ${b}`, "System.out.println"]
    );
}

// Phase 5: Multiplication (Levels 42 - 51) - 10 levels
for (let i = 42; i <= 51; i++) {
    let a = i - 40;
    let b = 7;
    let prod = a * b;
    addProblem(
        `Multiplication Pt ${i-41}`,
        "Multiplying numbers.",
        `Declare integers w = ${a} and z = ${b}. Print their product (w * z).`,
        "public class Solution {\n    public static void main(String[] args) {\n        // Type your answer below:\n        \n    }\n}",
        `${prod}`,
        [`w = ${a}`, `z = ${b}`, "System.out.println"]
    );
}

// Phase 6: If Statements (Levels 52 - 61) - 10 levels
for (let i = 52; i <= 61; i++) {
    let limit = Math.floor(i / 2) * 10;
    addProblem(
        `If Statements Pt ${i-51}`,
        "Using an if statement.",
        `If score is greater than ${limit}, print "Win". The score is ${limit + 5}.`,
        `public class Solution {\n    public static void main(String[] args) {\n        int score = ${limit + 5};\n        // Write an if statement below:\n        \n    }\n}`,
        "Win",
        ["if", `score > ${limit}`, `"Win"`]
    );
}

// Phase 7: For Loops (Levels 62 - 71) - 10 levels
for (let i = 62; i <= 71; i++) {
    let times = i - 60; // 2 to 11
    let word = words[(i-62) % words.length];
    
    // Build expected output with physical newlines instead of raw \n text so we can manipulate it
    let expArr = [];
    for(let k=0; k<times; k++) expArr.push(word);
    
    addProblem(
        `For Loops Pt ${i-61}`,
        "Looping a specific number of times.",
        `Write a for loop that prints "${word}" exactly ${times} times on separate lines.`,
        "public class Solution {\n    public static void main(String[] args) {\n        // Your loop here:\n        \n    }\n}",
        expArr.join("\\n"), // We store as raw text \n for JSON format builder
        ["for", `i < ${times}`, `"${word}"`]
    );
}

// Phase 8: While Loops (Levels 72 - 81) - 10 levels
for (let i = 72; i <= 81; i++) {
    let start = i - 70; // 2 to 11
    
    let expArr = [];
    for(let k=start; k>0; k--) expArr.push(k.toString());
    
    addProblem(
        `While Loops Pt ${i-71}`,
        "Looping downwards.",
        `Use a while loop to print numbers from ${start} down to 1.`,
        `public class Solution {\n    public static void main(String[] args) {\n        int count = ${start};\n        // Your while loop here:\n        \n    }\n}`,
        expArr.join("\\n"),
        ["while", "count >", "count--"]
    );
}

// Phase 9: Arrays (Levels 82 - 91) - 10 levels
for (let i = 82; i <= 91; i++) {
    let val = i * 2;
    addProblem(
        `Arrays Pt ${i-81}`,
        "Accessing array elements.",
        `Given int[] arr = {10, 20, 30}. Change index 1 to ${val}. Print arr[1].`,
        "public class Solution {\n    public static void main(String[] args) {\n        int[] arr = {10, 20, 30};\n        // Modify and print:\n        \n    }\n}",
        `${val}`,
        [`arr[1] = ${val}`, "System.out.println(arr[1])"]
    );
}

// Phase 10: Methods (Levels 92 - 100) - 9 levels
for (let i = 92; i <= 100; i++) {
    let val = i * 3;
    addProblem(
        `Methods Pt ${i-91}`,
        "Calling a method with parameters.",
        `Call printNumber(${val}) inside main().`,
        `public class Solution {\n    public static void printNumber(int x) {\n        System.out.println(x);\n    }\n    public static void main(String[] args) {\n        // Call printNumber below:\n        \n    }\n}`,
        `${val}`,
        [`printNumber(${val});`]
    );
}


// --- Generate Files ---
for (let i = 2; i <= 100; i++) {
    const levelNumberStr = i.toString().padStart(3, '0');
    const filePath = path.join(levelDataDir, `level_${levelNumberStr}.json`);
    
    let prob = uniqueProblems[i - 2];
    
    let baseReward = 100 + (Math.floor(i / 5) * 10);
    let reqLevel = i - 1;

    // Convert literal \\n in expected string to actual physical newlines for Unity
    let expectedOutputStr = prob.expected.replace(/\\\\n/g, "\n").replace(/\\n/g, "\n");

    const jsonObj = {
        levelNumber: i,
        levelName: `Level ${i}: ${prob.name}`,
        category: "Basics",
        difficulty: Math.floor(i / 10),
        puzzleDescription: prob.desc,
        objective: prob.obj,
        starterCode: prob.code,
        expectedOutput: expectedOutputStr,
        hints: [
            "Read the objective carefully.",
            "Make sure your syntax is correct.",
            "Check for missing semicolons."
        ],
        testCases: [],
        requiredKeywords: prob.keys,
        forbiddenKeywords: [],
        baseTokenReward: baseReward,
        perfectBonus: 50,
        speedBonus: 25,
        unlockedAchievements: [],
        sceneName: "GameScene_Template",
        requiredMechanics: ["terminal"],
        tokensToCollect: 3,
        isLocked: true,             
        requiredLevel: reqLevel
    };

    fs.writeFileSync(filePath, JSON.stringify(jsonObj, null, 4), 'utf8');
}

console.log(`Successfully generated 99 TRULY UNIQUE levels 2 to 100 in ${levelDataDir}`);
