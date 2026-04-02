"# codebound" 
"# capstone" 

## Level Validation Standard
- Use `expectedOutput` when the puzzle has a fixed console result.
- Use `expectedOutputPattern` when the puzzle has a fixed sentence shape but variable values.
- Use `requiredPrintlnCount` when the puzzle must contain a minimum number of `System.out.println(...)` statements but does not have a single fixed output string.
- Use `requiredCodePattern` for sentence-style levels that need an exact code structure but not a fixed final output value.
- Keep `requiredKeywords` for syntax or API checks, but do not rely on it alone for multi-line output tasks.
- Validation ignores comments, so commented-out keywords or `println` calls do not count.
- Validation also checks for obvious missing semicolons and undeclared identifiers in print, return, and assignment expressions.
