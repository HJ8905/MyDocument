//**********************************
//*ClassName:
//*Version:
//*Date:
//*Author:
//*Effect:
//**********************************

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OA.BLL
{
   public class TestClass
    {
        protected void btnChatRequest(object sender,EventArgs e)
        {
            Visitor v = new Visitor();
            Bll.ChatRequest(v);
        }

        protected void btnChatAccept(object sender, EventArgs e)
        {
            Agent A = new Agent();
            string Gid = string.Empty;
            Bll.ChatAccept(A,Gid);
        }

        protected void btnJoinChat(object sender, EventArgs e)
        {
            Agent A = new Agent();
            string Gid = string.Empty;
            Bll.ChatAccept(A, Gid);
        }

    }


    public class Bll
    {
        public static void ChatRequest(Visitor v)
        {
            ChatTeam ct = new ChatTeam
            {
                Visitor = v,
                Gid=System.Guid.NewGuid().ToString(),
                Enable=false
            };
            Dal.InsertChatTeam(ct);

        }

        public static void ChatAccept(Agent A,string Gid)
        {
            ChatTeam ct = Dal.GetChatTeamByGid(Gid);

            if (ct == null)
            {
                ct.Enable = true;
                ct.Agents.Add(A);
                Dal.UpdateChatTeam(ct);
            }
        }

        public static void JoinChat(Agent A, string Gid)
        {
            ChatTeam ct = Dal.GetChatTeamByGid(Gid,A);
            if (ct == null)
            {
                ct.Agents.Add(A);
                Dal.UpdateChatTeam(ct);
            }
        }

        public static void RemoveChat(Agent A, string Gid)
        {
            ChatTeam ct = Dal.GetChatTeamByGid(Gid,A);
            if (ct != null)
            {
                ct.Agents.Remove(A);

                Dal.UpdateChatTeam(ct);
            }
        }

        public static string SendMessage(Agent A, string Gid,string content)
        {
            ChatTeam ct = Dal.GetChatTeamByGid(Gid,A);
            if (ct != null)
            {
                ChatMessage cm = new ChatMessage
                {
                    TeamGid=Gid,
                    SendID=A.ID,
                    PersonType= Person.Agent,
                    Msg= content,
                    SendTime=DateTime.Now
                };
                Dal.InsertChatMessage(cm);
                return content;
            }
            else
            {
                return "该客服未在聊天列表中";
            }
        }

        public static string SendMessage(Visitor V, string Gid, string content)
        {
            ChatTeam ct = Dal.GetChatTeamByGid(Gid);
            if (ct != null)
            {
                if (ct.Visitor == V)
                {
                    ChatMessage cm = new ChatMessage
                    {
                        TeamGid = Gid,
                        SendID = V.ID,
                        PersonType = Person.Visitor,
                        Msg = content,
                        SendTime = DateTime.Now
                    };
                    Dal.InsertChatMessage(cm);
                    return content;
                }
                else
                {
                    return "访客不在聊天列表中";
                }
            }
            else
            {
                return "聊天尚未开启";
            }
        }

        public static List<ChatMessage> AcceptMessage(Visitor V, string Gid)
        {
            ChatTeam ct = Dal.GetChatTeamByGid(Gid);
            if (ct != null)
            {
                if (ct.Visitor == V)
                {
                    return Dal.AcceptMessage(Gid);
                }
                else
                {
                    return null;
                }
            }
            else
            {
                return null;
            }
        }

        public static List<ChatMessage> AcceptMessage(Agent A, string Gid)
        {
            ChatTeam ct = Dal.GetChatTeamByGid(Gid, A);
            if (ct != null)
            {
                return Dal.AcceptMessage(Gid);
            }
            else
            {
                JoinChat(A,Gid);
                return AcceptMessage(A, Gid);
            }
        }
    }

    public class Dal
    {
        public static void InsertChatTeam(ChatTeam ct)
        {

        }

        public static void UpdateChatTeam(ChatTeam ct)
        {

        }

        public static void InsertChatMessage(ChatMessage cm)
        {

        }
        public  static ChatTeam GetChatTeamByGid(string Gid)
        {
            List<ChatTeam> cts = new List<ChatTeam>();

            return cts.Where(t => t.Gid == Gid && t.Enable == false).FirstOrDefault();
        }

        public static ChatTeam GetChatTeamByGid(string Gid, Agent A)
        {
            List<ChatTeam> cts = new List<ChatTeam>();

            return cts.Where(t => t.Gid == Gid && t.Enable == false&&t.Agents.Contains(A)).FirstOrDefault();
        }

        public static List<ChatMessage> AcceptMessage(string Gid)
        {
            List<ChatMessage> icm = new List<ChatMessage>();
            return icm.Where(t => t.TeamGid == Gid).ToList();
        }
    }

    public class ChatMessage
    {
        public string TeamGid{ get; set; }
        public int SendID { get; set; }

        public Person PersonType { get; set; }
        public string Msg { get; set; }
        public DateTime SendTime { get; set; }
    }

    public class ChatTeam
    {
        public Visitor Visitor { get; set; }
        public List<Agent> Agents { get; set; }
        public string Gid { get; set; }
        public bool Enable { get; set; }
    }

    public class Visitor
    {
        public int ID { get; set; }
        public string Name { get; set; }

    }

    public class Agent
    {
        public int ID { get; set; }
        public string Name { get; set; }
    }


   public enum Person
    {
        Visitor,
        Agent
    }
}
