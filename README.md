#  Money Chat Bot (in progress )
MoneyBot is a web-based personal finance app built with ASP.NET Core and PostgreSQL. It uses Plaid API keys in sandbox mode to safely test connecting bank accounts and tracking live transactions. It features a smart chatbot using a Gemini API key alongside offline LINQ-driven fallback engine helping users easily view their spending habits, track savings,  manage monthly budgets , insights on 

All records are stored in PostgreSQL.   


## Bot Logic 

When a user asks a question in the chat interface, the application executes LINQ queries against the database to calculate real-time spending totals, category breakdowns, and savings insights.
Theses are then sent to the Gemini AI API with local LINQ fallbacks to deliver personalized, conversational banking  advise . 

1. ChatBotLogic:  offline, using  keyword-recognition  for financial insights using direct LINQ queries.
   
2. AiBotLogic: Handles AI conversation via GeminiService using the free  Gemini Api . It retrieves the data form   AppDbContext , queries it with LINQ and then sends it to Gemini as a prompt. If the user toggles Offline Mode OR if Gemini credits  run out, it  fallsback  to ChatBotLogic.
   
3. When a user asks a question, the application runs LINQ queries against PostgreSQL to returns facts: total outflow, monthly income, top categories, recurring charges, and top merchants across all active data sources.
4. 

   


   ## Tech stack
 PostgreSQL  - DB 
   C# 

