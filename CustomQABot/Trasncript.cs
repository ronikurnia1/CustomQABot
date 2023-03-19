using System.Collections.Generic;
using System.Text;

namespace CustomQABot;

public class Transcript
{
    public string Title { get; set; }
    public string Logo { get; set; }    
    public string Name { get; set; }
    public string DateTime { get; set; }
    public int NegativeFeedbackCount { get; set; } = 0;
    public StringBuilder PlainChats { get; set; } = new StringBuilder();
    public List<Chat> Chats { get; set; } = new List<Chat>();   
}


public class Chat
{
    public string Sender { get; set; }
    public string Message { get; set; }
}

