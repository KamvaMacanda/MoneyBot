using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MoneyBot.Data;
using OpenAI.Chat;


namespace MoneyBot.Services
{
    public class ChatToolService
    {
        private readonly AppDbContext _db;
        private readonly ChatClient _openAi;
        private readonly ChatCompletionOptions options = new();



        //Both tools take same two agrumwnts , so they share one schema 

        private const string DataRangeSchema = """
            {
            "type" : "object", 
            "properties" : { 
            "start" : { "type" : "string"} ,
            "end" : {"type" : "string"} }
            
            "required" : ["start" ,"end"]

            }
            """;



        /// <summary>
        /// Sets up the OpenAI client and tells the model which tools exist.
        /// The descriptions matter: the model reads them to decide which tool to call.
        /// </summary>
        public ChatToolService(AppDbContext db , IConfiguration config)
        {

            _db = db;
            _openAi = new ChatClient("gpt-40-mini", config["OpenAI:ApiKey"]);

            options.Tools.Add(ChatTool.CreateFunctionTool(
                "Spending by category",
                "Total spending per category between two dates ",
                BinaryData.FromString(DataRangeSchema)));

            options.Tools.Add(ChatTool.CreateFunctionTool(
                "Top merchants",
                "Top 10 merchants by total spend between two dats ",
                BinaryData.FromString(DataRangeSchema))); 
                

        }
         








        /// <summary>
        /// Called by ChatController for each user message. Sends the question to OpenAI.
        /// If the model asks for data, runs the tool and sends the result back,
        /// repeating until the model gives a final text answer.
        /// </summary> 
        ///  

        //Add more depth in the Prompt to openAi
        
        public async Task <string> PromptQuestion (string userId, string message)
        {
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(
                    $"Today is {DateTime.UtcNow:dd-MM-yyyy}. " +
                    $"You are a personal finance assitant.Only state numbers by tools." +
                    $"If you lack data , say so .For saving goals , propose specific cuts that add up " +
                    $"to the target and show the math ") ,
                new UserChatMessage(message)
            };

            while (true)
            {
                ChatCompletion reply = await _openAi.CompleteChatAsync(messages, options)).Value;

                if (reply.FinishReason != ChatFinishReason.ToolCalls)
                    return reply.Content[0].Text;

                messages.Add(new AssitantChatMessage(reply));

                foreach (var call in reply.ToolCalls)
                    messages.Add(new ToolChatMessage(call.Id, await RunToolAsync(userId, call))); 

            }
        }

        /// <summary>
        /// Total spent per category in the date range, biggest first.
        /// Skips transfers and loan payments, since that's money moving, not spending.
        /// </summary> 
        /// 
        private async Task<object> SpendingByCategoryAsync(string userId, DateOnly start, DateOnly end)
        {
            var skip = new[]
            {
                "Coming in" ,
                "Going Out",
                "Loan Payments"
            };

            return await _db.Transactions
                .Where(t => t.UserId == userId &&
                t.Date >= start &&
                t.Date <= end &&
                t.Amount > 0 && !skip.Contains(t.Category))

                .GroupBy(t => t.Category)

                .Select(g => new
                {
                    Category = g.Key,
                    Total = g.Sum(t => t.Amount)
                })


                .OrderByDescending(x => x.Total)

                .ToListAsync();

        }






        /// <summary>
        /// Runs the tool the model asked for and returns the result as JSON.
        /// userId comes from the logged-in user, never from the model,
        /// so a user can only ever query their own transactions.
        /// </summary>
        /// 

        public async Task <string> RunToolAsync (string userId, ChatToolCall call)
        {
            var args = JsonDocument.Parse(call.FunctionArguments).RootElement;
            var start = DateOnly.Parse(args.GetProperty("start").GetString()); 
            var end = DateOnly.Parse (args.GetProperty("end").GetString());

            object result = call.FunctionName switch
            {
                "spending by category" =>
               await SpendingByCategoryAsync(userId, start, end),

                "top_merchants" =>
                await TopMerchantsAsync(userId, start, end),

                _ => new { error = "unknown tool" }
            };

            return JsonSerializer.Serialize(result);
        }



        /// <summary>
        /// The 10 merchants you spent the most at in the date range.
        /// </summary> 
        /// 
        private async Task <object > TopMerchantsAsync (string userId , DateOnly start , DateOnly end)
        {

        }







    }
}
