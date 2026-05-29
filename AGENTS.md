# Project Documentation Rules

## Goal

Generate accurate UAT documentation and user manual for this application by using both:

1. The source code
2. The hosted UAT/staging system URL

## Rules

- Do not invent features.
- If a feature is unclear, mark it as "Needs business confirmation".
- Use the actual route names, page names, button names, and role names from the application.
- Cross-check workflows against the running UI using @Browser.
- Prefer practical business language over developer language.
- Separate admin workflows from normal user workflows.
- Include preconditions, test data, test steps, expected results, and pass/fail columns for UAT.
- For the user manual, write step-by-step instructions suitable for non-technical users.
- Save all documentation inside the docs/ folder.
- Do not modify application source code unless explicitly asked.

## Output files

Create or update:

- docs/uat/UAT_Test_Plan.md
- docs/uat/UAT_Test_Cases.md
- docs/uat/UAT_Roles_And_Permissions.md
- docs/uat/UAT_Test_Data.md
- docs/user-manual/User_Manual.md
- docs/user-manual/Admin_User_Manual.md
- docs/user-manual/FAQ.md
