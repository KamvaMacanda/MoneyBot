# Telemetry Money Chat Bot 

## System analysis and design  
MoneyBot is a server-driven ASP.NET Core web app  financial assistant that imports  transaction data using Plaid Link (encrypting access tokens and syncing live bank balances in ZAR) or through manual CSV statement uploads . 

All records are stored in PostgreSQL.   


## Bot Logic 

When a user asks a question in the chat interface, the application executes LINQ queries against the database to calculate real-time spending totals, category breakdowns, and savings insights.
Theses are then sent to the Gemini AI API with local LINQ fallbacks to deliver personalized, conversational banking  advise . 

1. ChatBotLogic:  offline, using  keyword-recognition  for financial insights and telemetry  using direct LINQ queries.
   
3. AiBotLogic: Handles AI conversation via GeminiService using the free  Gemini Api . It retrieves the data form   AppDbContext , queries it with LINQ and then sends it to Gemini as a prompt. If the user toggles Offline Mode OR if Gemini credits  run out, it  fallsback  to ChatBotLogic.

   ## Tech stack
   Supabase - hosts your PostgreSQL database in the cloud
   C# 

