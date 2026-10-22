# Telemetry Money Chat Bot 

## System analysis and design  
Telemetry bank chat bot using keyword recognition
MoneyBot is a server-driven ASP.NET Core web app  financial assistant that imports  transaction data using Plaid Link or through manual CSV statement uploads. 
Authentication is created using plaid sandbox to connect to bank accounts 

All records are stored in PostgreSQL.   


## Bot Logic 

When a user asks a question in the chat interface, the application executes LINQ queries against the database to calculate real-time spending totals, category breakdowns, and savings insights.
Theses are then sent to the Gemini AI API with local LINQ fallbacks to deliver personalized, conversational banking  advise . 

1. ChatBotLogic:  offline, using  keyword-recognition  for financial insights and telemetry  using direct LINQ queries.
   
2. AiBotLogic: Handles AI conversation via GeminiService using the free  Gemini Api . It retrieves the data form   AppDbContext , queries it with LINQ and then sends it to Gemini as a prompt. If the user toggles Offline Mode OR if Gemini credits  run out, it  fallsback  to ChatBotLogic.
   
3. When a user asks a question, the application runs LINQ queries against PostgreSQL to returns facts: total outflow, monthly income, top categories, recurring charges, and top merchants across all active data sources.
4. 

   


   ## Tech stack
   Supabase - hosts your PostgreSQL database in the cloud
   C# 

