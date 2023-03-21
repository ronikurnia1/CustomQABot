using System.Collections.Generic;

namespace CustomQABot;

public class Feedback
{
    public string Title { get; set; }
    public string Details { get; set; }
    public string Logo { get; set; }    
    public string Name { get; set; }
    public string DateTime { get; set; }
    public int NegativeFeedbackCount { get; set; } = 0;
    public List<Chat> Chats { get; set; } = new List<Chat>();
}


public class Chat
{
    public string Sender { get; set; }
    public string Message { get; set; }
}

