const args = process.argv.slice(2);

let maxIterations = 10;
let promptFile = "prompt.md";
let issuesFile = "issues.json";
let progressFile = "progress.txt";

for (let i = 0; i < args.length; i += 1) {
  const arg = args[i];
  if (arg === "--max-iterations") {
    const value = args[i + 1];
    if (!value || Number.isNaN(Number(value)) || Number(value) < 1) {
      console.error("ERROR: --max-iterations must be an integer >= 1");
      process.exit(2);
    }
    maxIterations = Number(value);
    i += 1;
    continue;
  }
  if (arg === "--prompt-file") {
    const value = args[i + 1];
    if (!value) {
      console.error("ERROR: --prompt-file is required");
      process.exit(2);
    }
    promptFile = value;
    i += 1;
    continue;
  }
  if (arg === "--issues-file") {
    const value = args[i + 1];
    if (!value) {
      console.error("ERROR: --issues-file is required");
      process.exit(2);
    }
    issuesFile = value;
    i += 1;
    continue;
  }
  if (arg === "--progress-file") {
    const value = args[i + 1];
    if (!value) {
      console.error("ERROR: --progress-file is required");
      process.exit(2);
    }
    progressFile = value;
    i += 1;
    continue;
  }
}

const prompt = await Bun.file(promptFile).text();
const issuesExists = await Bun.file(issuesFile).exists();
const issues = issuesExists ? await Bun.file(issuesFile).text() : "[]";
const progressExists = await Bun.file(progressFile).exists();
const progress = progressExists ? await Bun.file(progressFile).text() : "";

const combinedPrompt = buildCombinedPrompt(prompt, issues, progress);

let output = "";
for (let i = 1; i <= maxIterations; i += 1) {
  output = "<promise>COMPLETE</promise>";
  const entry = `\n\n---\n# Iteration ${i} (${new Date().toISOString()})\n\n${output}\n`;
  await Bun.write(progressFile, progress + entry);
  if (output.includes("<promise>COMPLETE</promise>")) {
    break;
  }
}

console.log(combinedPrompt.length > 0 ? "" : "");

function buildCombinedPrompt(promptTemplate: string, issuesJson: string, progressText: string) {
  const lines = [];
  lines.push("You are running inside a loop. Use the files and repository as your source of truth.");
  lines.push("Stop condition: when everything is done, output EXACTLY: <promise>COMPLETE</promise>.");
  lines.push("");
  lines.push("# ISSUES_JSON");
  lines.push("```json");
  lines.push(issuesJson.trim());
  lines.push("```");
  lines.push("");
  lines.push("# PROGRESS_SO_FAR");
  lines.push("```text");
  lines.push(progressText.trim() === "" ? "(empty)" : progressText.trim());
  lines.push("```");
  lines.push("");
  lines.push("# INSTRUCTIONS");
  lines.push(promptTemplate.trim());
  lines.push("");
  lines.push("# OUTPUT_RULES");
  lines.push("- If you are done, output EXACTLY: <promise>COMPLETE</promise>");
  lines.push("- Otherwise, output what you changed and what you will do next iteration.");
  return lines.join("\n");
}
