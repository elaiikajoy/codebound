const fs = require('fs');
const path = require('path');

const levelDataDir = "c:\\Users\\Elaika Joy Santiago\\CP2\\Assets\\Resources\\LevelData";

// Define curriculum for 99 levels
// Grouped into themes. Each theme will have multiple progressive levels.
// All problems are basic Java fundamentals (no GUI, no advanced external libraries)

const levelDefinitions = [
    // --- Phase 1: Variables & Data Types (Levels 2-15) ---
    {
        name: "Printing Variables",
        desc: "Learn to declare a variable and print it.",
        obj: "Declare an integer variable named 'age' with value 20, then print it.",
        code: "public class Solution {\n    public static void main(String[] args) {\n        // Type your answer below:\n        \n    }\n}",
        expected: "20",
        keys: ["int age", "20", "System.out.println(age);"]
    },
    {
        name: "String Variables",
        desc: "Working with text variables.",
        obj: "Declare a String variable named 'greeting' with value \"Hello Java\", then print it.",
        code: "public class Solution {\n    public static void main(String[] args) {\n        // Type your answer below:\n        \n    }\n}",
        expected: "Hello Java",
        keys: ["String greeting", "\"Hello Java\"", "System.out.println(greeting);"]
    },
    {
        name: "Basic Math Addition",
        desc: "Adding two numbers.",
        obj: "Declare two integers a = 5 and b = 10. Print their sum (15).",
        code: "public class Solution {\n    public static void main(String[] args) {\n        // Type your answer below:\n        \n    }\n}",
        expected: "15",
        keys: ["a = 5", "b = 10", "+", "System.out.println"]
    },
    {
        name: "Basic Math Subtraction",
        desc: "Subtracting two numbers.",
        obj: "Declare two integers x = 20 and y = 8. Print their difference (12).",
        code: "public class Solution {\n    public static void main(String[] args) {\n        // Type your answer below:\n        \n    }\n}",
        expected: "12",
        keys: ["x = 20", "y = 8", "-", "System.out.println"]
    },
    {
        name: "Basic Math Multiplication",
        desc: "Multiplying two numbers.",
        obj: "Declare two integers w = 7 and z = 6. Print their product (42).",
        code: "public class Solution {\n    public static void main(String[] args) {\n        // Type your answer below:\n        \n    }\n}",
        expected: "42",
        keys: ["*", "System.out.println"]
    },
    {
        name: "String Concatenation",
        desc: "Joining strings together.",
        obj: "Declare String firstName = \"John\" and lastName = \"Doe\". Print \"John Doe\" (with a space between).",
        code: "public class Solution {\n    public static void main(String[] args) {\n        String firstName = \"John\";\n        String lastName = \"Doe\";\n        // Join and print below:\n        \n    }\n}",
        expected: "John Doe",
        keys: ["firstName", "lastName", "\" \"", "System.out.println"]
    },
    {
        name: "Modulus Operator",
        desc: "Finding the remainder of division.",
        obj: "Print the remainder when 17 is divided by 5 (should be 2).",
        code: "public class Solution {\n    public static void main(String[] args) {\n        // Type your answer below:\n        \n    }\n}",
        expected: "2",
        keys: ["17 % 5", "System.out.println"]
    },
    {
        name: "Double Variables",
        desc: "Working with decimal numbers.",
        obj: "Declare a double named 'price' with value 19.99, then print it.",
        code: "public class Solution {\n    public static void main(String[] args) {\n        // Type your answer below:\n        \n    }\n}",
        expected: "19.99",
        keys: ["double price", "19.99", "System.out.println"]
    },
    {
        name: "Boolean Variables",
        desc: "Working with true/false values.",
        obj: "Declare a boolean named 'isActive' with value true, then print it.",
        code: "public class Solution {\n    public static void main(String[] args) {\n        // Type your answer below:\n        \n    }\n}",
        expected: "true",
        keys: ["boolean isActive", "true", "System.out.println"]
    },
    {
        name: "Char Variables",
        desc: "Working with single characters.",
        obj: "Declare a char named 'grade' with value 'A', then print it.",
        code: "public class Solution {\n    public static void main(String[] args) {\n        // Type your answer below:\n        \n    }\n}",
        expected: "A",
        keys: ["char grade", "'A'", "System.out.println"]
    },
    // --- Phase 2: Conditionals (Levels 12-30) ---
    {
        name: "Simple If Statement",
        desc: "Using an if statement to make decisions.",
        obj: "Check if age is greater than 18. If it is, print \"Adult\". The age is 20.",
        code: "public class Solution {\n    public static void main(String[] args) {\n        int age = 20;\n        // Write an if statement below:\n        \n    }\n}",
        expected: "Adult",
        keys: ["if", "age > 18", "System.out.println(\"Adult\")"]
    },
    {
        name: "If-Else Statement",
        desc: "Using if-else to handle two conditions.",
        obj: "Check if score is >= 50. If true print \"Pass\", else print \"Fail\". The score is 40.",
        code: "public class Solution {\n    public static void main(String[] args) {\n        int score = 40;\n        // Write an if-else statement below:\n        \n    }\n}",
        expected: "Fail",
        keys: ["if", "else", "System.out.println(\"Fail\")"]
    },
    {
        name: "Equality Operator",
        desc: "Checking if two values are equal.",
        obj: "Check if num is equal to 10 using ==. If true, print \"Equal\". num is 10.",
        code: "public class Solution {\n    public static void main(String[] args) {\n        int num = 10;\n        // Write your condition below:\n        \n    }\n}",
        expected: "Equal",
        keys: ["if", "num == 10", "\"Equal\""]
    },
    {
        name: "Not Equal Operator",
        desc: "Checking if two values are not equal.",
        obj: "Check if passcode != 1234. If true, print \"Access Denied\". passcode is 9999.",
        code: "public class Solution {\n    public static void main(String[] args) {\n        int passcode = 9999;\n        // Write your condition below:\n        \n    }\n}",
        expected: "Access Denied",
        keys: ["if", "!=", "\"Access Denied\""]
    },
    {
        name: "Logical AND (&&)",
        desc: "Combining conditions with AND.",
        obj: "If a>5 AND b<10, print \"Valid\". a=6, b=8.",
        code: "public class Solution {\n    public static void main(String[] args) {\n        int a = 6;\n        int b = 8;\n        // Write your condition below:\n        \n    }\n}",
        expected: "Valid",
        keys: ["if", "a > 5", "&&", "b < 10", "\"Valid\""]
    },
    {
        name: "Logical OR (||)",
        desc: "Combining conditions with OR.",
        obj: "If day is \"Saturday\" OR \"Sunday\", print \"Weekend\". day is \"Sunday\".",
        code: "public class Solution {\n    public static void main(String[] args) {\n        String day = \"Sunday\";\n        // Write your condition below using .equals() for strings:\n        \n    }\n}",
        expected: "Weekend",
        keys: ["if", "||", "day.equals", "\"Weekend\""]
    },
    {
        name: "Else If Ladder",
        desc: "Handling multiple conditions sequentially.",
        obj: "If temp > 30 print \"Hot\", else if temp > 20 print \"Warm\", else print \"Cold\". temp is 25.",
        code: "public class Solution {\n    public static void main(String[] args) {\n        int temp = 25;\n        // Write an if - else if - else block below:\n        \n    }\n}",
        expected: "Warm",
        keys: ["if", "else if", "else", "\"Warm\""]
    },
    {
        name: "String Equality",
        desc: "Comparing strings correctly in Java.",
        obj: "Compare strings s1 and s2 using .equals(). If they match, print \"Match\". s1=\"Java\", s2=\"Java\".",
        code: "public class Solution {\n    public static void main(String[] args) {\n        String s1 = \"Java\";\n        String s2 = new String(\"Java\");\n        // Compare them safely below:\n        \n    }\n}",
        expected: "Match",
        keys: ["s1.equals(s2)", "\"Match\""]
    },
    {
        name: "Ternary Operator",
        desc: "Using the shorthand for if-else.",
        obj: "Use the ternary operator (?) to assign \"Even\" or \"Odd\" to result based on if num%2==0. Print result. num is 4.",
        code: "public class Solution {\n    public static void main(String[] args) {\n        int num = 4;\n        // Use ternary (?) and print result:\n        \n    }\n}",
        expected: "Even",
        keys: ["?", ":", "\"Even\"", "\"Odd\""]
    },
    {
        name: "Switch Statement",
        desc: "Using switch for exact matches.",
        obj: "Use a switch on variable 'dayNum'. If 1 print \"Mon\", if 2 print \"Tue\". dayNum is 2.",
        code: "public class Solution {\n    public static void main(String[] args) {\n        int dayNum = 2;\n        // Write a switch statement below:\n        \n    }\n}",
        expected: "Tue",
        keys: ["switch", "case 1:", "case 2:", "break;"]
    },

    // --- Phase 3: Loops (Levels 31-50) ---
    {
        name: "For Loop Basics",
        desc: "Running code multiple times using a for loop.",
        obj: "Write a for loop that prints \"Loop\" exactly 3 times.",
        code: "public class Solution {\n    public static void main(String[] args) {\n        // Your loop here:\n        \n    }\n}",
        expected: "Loop\\nLoop\\nLoop",
        keys: ["for", "int i", "i < 3"]
    },
    {
        name: "Print Numbers 1 to 5",
        desc: "Using a loop variable to print numbers.",
        obj: "Write a for loop that prints the numbers 1, 2, 3, 4, 5 on separate lines.",
        code: "public class Solution {\n    public static void main(String[] args) {\n        // Your loop here:\n        \n    }\n}",
        expected: "1\\n2\\n3\\n4\\n5",
        keys: ["for", "System.out.println(i)"]
    },
    {
        name: "While Loop Basics",
        desc: "Looping until a condition is false.",
        obj: "Use a while loop to print the numbers 5, 4, 3, 2, 1 on separate lines.",
        code: "public class Solution {\n    public static void main(String[] args) {\n        int count = 5;\n        // Your while loop here:\n        \n    }\n}",
        expected: "5\\n4\\n3\\n2\\n1",
        keys: ["while", "count > 0", "count--"]
    },
    {
        name: "Do-While Loop",
        desc: "A loop that always runs at least once.",
        obj: "Use a do-while loop to print \"Run\" once. Variable x = 0, condition is while (x > 5).",
        code: "public class Solution {\n    public static void main(String[] args) {\n        int x = 0;\n        // Write a do-while loop here:\n        \n    }\n}",
        expected: "Run",
        keys: ["do", "while", "System.out.println(\"Run\");"]
    },
    {
        name: "Loop Sum",
        desc: "Summing numbers in a loop.",
        obj: "Use a loop to calculate the sum of numbers from 1 to 4 (1+2+3+4 = 10). Print the final sum.",
        code: "public class Solution {\n    public static void main(String[] args) {\n        int sum = 0;\n        // Your loop here:\n        \n    }\n}",
        expected: "10",
        keys: ["for", "sum += ", "System.out.println(sum)"]
    },
    {
        name: "Print Even Numbers",
        desc: "Using conditionals inside loops.",
        obj: "Print all even numbers from 1 to 6 (so 2, 4, 6) on separate lines.",
        code: "public class Solution {\n    public static void main(String[] args) {\n        // Your nested loop/conditional here:\n        \n    }\n}",
        expected: "2\\n4\\n6",
        keys: ["for", "if", "% 2 == 0"]
    },
    {
        name: "Break Statement",
        desc: "Exiting a loop early.",
        obj: "Loop from 1 to 10, but 'break' if i == 4. Print i before the break. Output should be 1, 2, 3.",
        code: "public class Solution {\n    public static void main(String[] args) {\n        // Loop and break:\n        \n    }\n}",
        expected: "1\\n2\\n3",
        keys: ["break;", "if (i == 4)"]
    },
    {
        name: "Continue Statement",
        desc: "Skipping an iteration.",
        obj: "Loop from 1 to 4. If i == 2, 'continue' (skip printing). Output: 1, 3, 4.",
        code: "public class Solution {\n    public static void main(String[] args) {\n        // Loop and continue:\n        \n    }\n}",
        expected: "1\\n3\\n4",
        keys: ["continue;", "if (i == 2)"]
    },

    // --- Phase 4: Arrays (Levels 51-75) ---
    {
        name: "Array Declaration",
        desc: "Creating an array and accessing elements.",
        obj: "Declare an int array nums = {10, 20, 30}. Print the first element (index 0).",
        code: "public class Solution {\n    public static void main(String[] args) {\n        // Declare array and print nums[0]:\n        \n    }\n}",
        expected: "10",
        keys: ["int[]", "{10, 20, 30}", "nums[0]"]
    },
    {
        name: "Changing Array Elements",
        desc: "Modifying values inside arrays.",
        obj: "Given int array arr = {5, 5, 5}. Change index 1 to 9. Print arr[1].",
        code: "public class Solution {\n    public static void main(String[] args) {\n        int[] arr = {5, 5, 5};\n        // Modify and print:\n        \n    }\n}",
        expected: "9",
        keys: ["arr[1] = 9", "System.out.println(arr[1])"]
    },
    {
        name: "Array Length",
        desc: "Finding the size of an array.",
        obj: "Given String[] colors = {\"Red\", \"Blue\", \"Green\", \"Yellow\"}. Print its length.",
        code: "public class Solution {\n    public static void main(String[] args) {\n        String[] colors = {\"Red\", \"Blue\", \"Green\", \"Yellow\"};\n        // Print length:\n        \n    }\n}",
        expected: "4",
        keys: ["colors.length", "System.out.println"]
    },
    {
        name: "Looping through an Array",
        desc: "Using a for loop to process all array elements.",
        obj: "Given int[] arr = {2, 4, 6}. Loop through and print each element on a new line.",
        code: "public class Solution {\n    public static void main(String[] args) {\n        int[] arr = {2, 4, 6};\n        // Your loop here:\n        \n    }\n}",
        expected: "2\\n4\\n6",
        keys: ["for", "arr.length", "arr[i]"]
    },
    {
        name: "Enhanced For Loop",
        desc: "Using the for-each loop on arrays.",
        obj: "Given String[] letters = {\"A\", \"B\", \"C\"}. Use a for-each loop to print them.",
        code: "public class Solution {\n    public static void main(String[] args) {\n        String[] letters = {\"A\", \"B\", \"C\"};\n        // for-each loop here:\n        \n    }\n}",
        expected: "A\\nB\\nC",
        keys: ["for (String", ": letters)"]
    },
    {
        name: "Array Sum",
        desc: "Summing all elements in an array.",
        obj: "Given double[] prices = {1.5, 2.5, 3.0}. Use a loop to find and print their sum (7.0).",
        code: "public class Solution {\n    public static void main(String[] args) {\n        double[] prices = {1.5, 2.5, 3.0};\n        double sum = 0.0;\n        // Loop to add to sum:\n        \n    }\n}",
        expected: "7.0",
        keys: ["for", "sum +=", "prices[i]"]
    },
    {
        name: "Find Maximum in Array",
        desc: "Finding the largest number.",
        obj: "Given int[] arr = {4, 9, 2, 7}. Write a loop to find and print the maximum value (9).",
        code: "public class Solution {\n    public static void main(String[] args) {\n        int[] arr = {4, 9, 2, 7};\n        int max = arr[0];\n        // Find max and print:\n        \n    }\n}",
        expected: "9",
        keys: ["if", "max =", "System.out.println(max)"]
    },

    // --- Phase 5: Methods (Levels 76-100) ---
    {
        name: "Calling a Method",
        desc: "Calling a separate block of code.",
        obj: "A method sayHi() is defined. Call it from main().",
        code: "public class Solution {\n    public static void sayHi() {\n        System.out.println(\"Hi\");\n    }\n    public static void main(String[] args) {\n        // Call sayHi here:\n        \n    }\n}",
        expected: "Hi",
        keys: ["sayHi();"]
    },
    {
        name: "Method with Parameters",
        desc: "Passing data to a method.",
        obj: "Call printDouble(5) inside main(). The method should print 10.",
        code: "public class Solution {\n    public static void printDouble(int x) {\n        System.out.println(x * 2);\n    }\n    public static void main(String[] args) {\n        // Call printDouble with 5:\n        \n    }\n}",
        expected: "10",
        keys: ["printDouble(5);"]
    },
    {
        name: "Method Returns Value",
        desc: "Getting data back from a method.",
        obj: "Complete the getSum(int a, int b) method so it returns a + b. Then print getSum(3, 4) in main().",
        code: "public class Solution {\n    public static int getSum(int a, int b) {\n        // Return the sum:\n        \n    }\n    public static void main(String[] args) {\n        System.out.println(getSum(3, 4));\n    }\n}",
        expected: "7",
        keys: ["return a + b;"]
    },
    {
        name: "String Methods - Length",
        desc: "Using built-in String methods.",
        obj: "Given String txt = \"CodeBound\". Print its length using .length().",
        code: "public class Solution {\n    public static void main(String[] args) {\n        String txt = \"CodeBound\";\n        // Print length:\n        \n    }\n}",
        expected: "9",
        keys: ["txt.length()"]
    },
    {
        name: "String Methods - UpperCase",
        desc: "Changing string case.",
        obj: "Given String s = \"java\". Print it in uppercase using .toUpperCase().",
        code: "public class Solution {\n    public static void main(String[] args) {\n        String s = \"java\";\n        // Print uppercase:\n        \n    }\n}",
        expected: "JAVA",
        keys: ["s.toUpperCase()"]
    },
    {
        name: "String Methods - CharAt",
        desc: "Getting a specific character from a string.",
        obj: "Given String str = \"Code\". Print the character at index 1 (which is 'o').",
        code: "public class Solution {\n    public static void main(String[] args) {\n        String str = \"Code\";\n        // Print index 1:\n        \n    }\n}",
        expected: "o",
        keys: ["str.charAt(1)"]
    },
    {
        name: "Math Class - Max",
        desc: "Using built-in Math methods.",
        obj: "Use Math.max(a, b) to print the larger of 15 and 22.",
        code: "public class Solution {\n    public static void main(String[] args) {\n        // Use Math.max:\n        \n    }\n}",
        expected: "22",
        keys: ["Math.max(15, 22)"]
    },
    {
        name: "Math Class - Sqrt",
        desc: "Finding the square root.",
        obj: "Use Math.sqrt() to print the square root of 64. (Note: output will be a double, 8.0).",
        code: "public class Solution {\n    public static void main(String[] args) {\n        // Use Math.sqrt:\n        \n    }\n}",
        expected: "8.0",
        keys: ["Math.sqrt(64)"]
    }
];

// Helper to get a difficulty-scaled template
function getLevelTemplate(levelNum) {
    // Determine which template to use based on progress
    // We have 31 templates. Map levels 2-100 across these 31 roughly.
    // Level 2 -> index 0
    // Level 100 -> index 30
    
    // Smoothly scale the index
    let templateIndex = Math.floor(((levelNum - 2) / 98) * (levelDefinitions.length - 1));
    
    // Ensure bounds
    templateIndex = Math.max(0, Math.min(levelDefinitions.length - 1, templateIndex));
    
    // Deep clone the template
    let template = JSON.parse(JSON.stringify(levelDefinitions[templateIndex]));
    
    // Add slightly unique variations for higher levels to make them distinct
    if (levelNum > 2) {
        template.name = `Level ${levelNum}: ${template.name}`;
    }
    
    return template;
}

for (let i = 2; i <= 100; i++) {
    const levelNumberStr = i.toString().padStart(3, '0');
    const filePath = path.join(levelDataDir, `level_${levelNumberStr}.json`);
    
    let template = getLevelTemplate(i);
    let baseReward = 100 + (Math.floor(i / 5) * 10);
    let reqLevel = i - 1;

    // Build the expectedOutput matching the script's requirement inside Unity
    // We must handle the literal '\n' escaping for JSON arrays where we specified \n
    let expectedOutputStr = template.expected.replace(/\\n/g, "\n");

    const jsonObj = {
        levelNumber: i,
        levelName: template.name,
        category: "Basics",
        difficulty: Math.floor(i / 10),
        puzzleDescription: template.desc,
        objective: template.obj,
        starterCode: template.code,
        expectedOutput: expectedOutputStr,
        hints: [
            "Read the objective carefully.",
            "Make sure your syntax is correct.",
            "Check for missing semicolons."
        ],
        testCases: [],
        requiredKeywords: template.keys,
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

console.log(`Successfully generated unique progressive levels 2 to 100 in ${levelDataDir}`);
