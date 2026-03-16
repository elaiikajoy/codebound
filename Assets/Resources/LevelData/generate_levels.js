const fs = require('fs');
const path = require('path');

const levelDataDir = "c:\\Users\\Elaika Joy Santiago\\CP2\\Assets\\Resources\\LevelData";

for (let i = 2; i <= 100; i++) {
    const levelNumberStr = i.toString().padStart(3, '0');
    const filePath = path.join(levelDataDir, `level_${levelNumberStr}.json`);
    
    let levelName = `Level ${i}: Hello Java`;
    let baseReward = 100 + (i * 10);
    let reqLevel = i - 1;

    const jsonObj = {
        levelNumber: i,
        levelName: levelName,
        category: "Basics",
        difficulty: 0,
        puzzleDescription: `Welcome to your Java coding task for level ${i}! In Java, we print messages to the console using 'System.out.println()'.`,
        objective: `Use System.out.println to print exactly "Level ${i} Passed" to the console.`,
        starterCode: `public class Solution {\n    public static void main(String[] args) {\n        // Type your answer below this line:\n        \n        \n    }\n}`,
        expectedOutput: `Level ${i} Passed`,
        hints: [
            "Check your spelling and capitalization carefully.",
            "Remember that statements in Java must end with a semicolon (;).",
            `Make sure your text is inside quotes: "Level ${i} Passed"`
        ],
        testCases: [],
        requiredKeywords: [
            "System.out.println",
            `"Level ${i} Passed"`
        ],
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

console.log(`Successfully generated levels 2 to 100 in ${levelDataDir}`);
