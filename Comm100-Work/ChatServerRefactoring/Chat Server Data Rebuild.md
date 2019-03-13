
# Chat Server 数据重建设计

## 1. 背景

新版本的chatserver将支持**Loading Blance**, chatserver通过集群的方式对外提供服务。某个具体站点的`Chat`均会根据算法被分配到的固定的chatserver实例上。
如果某个chatserver实例发生故障这个实例上的chat将会被自动切换到另一个可用的实例上。为保障客户的业务不发生中断必须把相应的`CurrentOperator`、`CurrentVisitor`、`Chat` 在新的chatserver实例中进行恢复。
chat保存的时候要注意GUID是否存在.
**Agent 和Agent 聊天**

## 2. 总体思路

+ 聊天有3个关联对象: `CurrentOperator`(可能有多个)、`CurrentVisitor`、`Chat`, 只有这3个对象都已经在新的Chat Server 实例上完成恢复后聊天才可以继续进行, 恢复完成以前相关接口都会收到服务器的错误信息并做retry 直至数据恢复完成。
+ Agent Console 的`Heartbeat`接口会恢复`CurrentOperator`、`CurrentVisitor`、`Chat` 3个对象, 访客端的`Heartbeat`接口只会恢复`Chat.ChatMessage`对象。
+ 一个聊天可能会有多个`CurrentOperator`参与, 不同的Agent Console 本地保存的`CurrentVisitor`和`Chat`对象可能存在版本差异, 为保证最终恢复的`CurrentVisitor`和`Chat`版本是最新的, 给`CurrentVisitor`对象添加`OffSet`属性, 给`Chat`对象添加`CurrentChatMessagesVersion`属性来标识版本, 只有本地版本比较新时才会覆盖服务器版本。
+ `CurrentOperator` 也添加`Offset`属性来标识版本。具体的的使用场景：
  1. Agent 先在chatserver A 聊天,  chatserver A 有对应的`CurrentOperator`对象。
  2. ChatServer A 故障, 聊天切换到chatserver B, `CurrentOperator` 在chatserver B 恢复。
  3. 在chatserver B 聊天时`CurrentOperator`对象的一些属性发生了变化, 例如`Status`从Online 变成了`Away`。
  4. chatserver A 可以正常运作, 聊天切换回chatserver A, 此时因为chatserver A 可能还存有Agent 对应的`CurrentOperator`对象，如果没有`Offset`就无法确定是否需要重新恢复一次`CurrentOperator`。
+ Agent Console 和Visitor Side 在发送请求时会带上对应的`Offset`和`CurrentChatMessagesVersion`，chatserver 根据标识来确定是否需要返回对应的错误码并进行数据恢复。
  
## 3 详细设计

### 3.1 数据结构

```c#
   //新增对象.
   public class EndedChat
   {
      public string ChatGuid;
      public DateTime EndTime;
   }

   //原有对象, 添加新属性.
   public class ActiveSite
   {
      /*当有聊天结束时, 把ChatGuid 加入到这个队列, 超过10分钟就会被清除.
       有时候服务端某个聊天已结束, 但是Visitor 和 Agent Console 还未收到消息，客户端发送心跳时会报'ChatId`不存在,这时如果根据`ChatId`不存在就进行
       数据恢复是不对的。解决方案是把刚结束的ChatId 缓存一下, 不仅要判断传过来的ChatId不属于任何正在进行的聊天,还要排除聊天可能刚结束的情况.
      */
      public List<EndedChat> EndedChatList;
   }

   //原有对象, 添加新属性.
   public class CurrentOperator
   {
      public int offset; //当Operator 的属性发生变化时, offSet++;
   }

   //原有对象, 添加新属性.
   public class CurrentVisitor
   {
      public int offset; //当Visitor 的属性发生变化时, offset++;
   }

   //原有对象, 添加新属性.
   public class Chat
   {
      public string chatVersion
      {
       return _chatMessages[_chatMessages - 1]._messageGuid;
      }
   }

   //原有对象, 添加新属性.
   public enum FrameworkErrorCode
   {
      enumSystemNeedRecoverChat = 40,
   }

```

### 3.2 系统流程

#### 3.1 Site对应的chatserver实例发生故障,聊天被切换

1. Agent Console 和Visitor Side 的Request 都会返回`enumSystemNeedRecoverData`错误.
   + 因为新的chatserver 不存在对应的`CurrentOperator`, （请求里带的operatorId 在服务器端不存在, 但是Agent Console 传过来的token 经过校验又是合法的**这个地方是不是还有遗漏?**）所以Agent Console除了登录以外的请求都会失败, 需要retry 直至数据被恢复。
   + 如果访客并不处于聊天状态, 相关请求是可以正常返回的(例如 request chat, page visitor, pre-chat 等), 如果切换时处于聊天状态，相关的request 也会收到`enumSystemNeedRecoverData` 并做retry 直至数据恢复。
   + Question **正在请求聊天的怎么处理?**
  
2. Agent Console 在`enumOperatorGetMessagesFormAllChats`接口恢复`CurrentOperator`,`Visitor`, `Chat`
   + chatserver 收到`enumOperatorGetMessagesFormAllChats` 请求时如果发现如下几种情况均返回`enumSystemNeedRecoverData`错误 **关注一下这个**
     1. Agent Console 传过来的token 校验通过, 但对应的OperatorId在服务器中不存在.
     2. `operatorId`存在, 但local的`CurrentOperator.Offset`> 服务器的`CurrentOperator.Offset`
     3. 对应的`VisitorGuid`在服务器端不存在或者local 的`CurrentVisitor.Offset` > 服务器的`CurrentVisitor.Offset`
     4. 传过来的`ChatGuid`在服务端不存在或local的`Chat.CurrentChatMessagesVersion`在服务器端不存在。
   + Agent Console 收到`enumSystemNeedRecoverData`错误后立即调用 `enumOperatorRecoverChat`接口恢复数据（`Agent`->`Visitor`->`Chat`）,Agent Console会在本地添加一个全局变量`IsRecoveringChat`,在调用`enumOperatorRecoverChat`   接口的过程中`IsRecoveringChat`一直为`True`,只要这个变量是true,Agent Console 就不会再次调用`enumOperatorRecoverChat`接口。
   + 如果一个`Chat`涉及多个`CurrentOperator`，同一个`CurrentVisitor`和`Chat`可能会恢复多次, 第2次恢复时会再次比较`Offset`和`Chat.CurrentChatMessagesVersion`确保服务端恢复的是最新版本。

3. 访客端调用的和聊天相关的ChatServer接口都要传chatVersion 字段, chatVersion 就是访客端存储的最新一条`ChatMessage`的`_messageGuid`.
   如果chatserver 返回`enumSystemNeedRecoverData`错误, 访客端进行retry操作直至数据恢复成功.
   这些接口包括(红色代表不确定, 要和lynn和Roy确认一下):
   + $\color{red}GetCustomVariable$
   + $\color{red}setCustomVariable$
   + $\color{red}SetPushConfig$
   + $\color{red}SetIsNeedPush$
   + SetLocation
   + $\color{red}CheckDynamicCampaign, GetDynamicCampaign$
   + $\color{red}CheckBan$
   + $\color{red}CheckIfOnline$
   + $\color{red}GetAgents$
   + ChatBotSelectQuestion
   + ChatBotAnswerHelpful
   + SendFile
   + VisitorHandlerBotForm
   + GetVisitorInfo
   + $\color{red}CheckBBSCode$
   + $\color{red}GetVisitorInfo$
   + $\color{red}RestoreChat$
   + $\color{red}AgentMsgSeenByVisitor（这个好像不是chatting状态也能发）$ 
   + $\color{red}Batch$
   + $\color{red}CheckManullInvitation$
   + $\color{red}CheckManullInvited$
   + $\color{red}EmailTranscript (这个要重点讨论一下)$
   + EndChat
   + GetChatMessages
   + SendChatMessages
   + $\color{red}SocialLogin$
   + $\color{red}SocialLogin$

4. Visitor 在调用`getChatMessages`接口时如果收到`enumSystemNeedRecoverData`错误就调用`RecoverChat`接口恢复聊天消息
   + chatserver 收到`getChatMessages`请求时:
     1. 对应的`CurrentVisitor`或`Chat`不存在, 直接返回 (等Agent Console 恢复)。
     2. 对应的`CurrentVisitor`和`Chat`均存在,但是本地的`chatVersion`在服务器端不存在就返回`enumSystemNeedRecoverData`。
     3. 版本正确, 正常执行相关操作.
     4. `RecoverChat` 接口要传如下参数:

```c#
    //固定字段，其他访客端的接口也有这些字段
    public int siteId;
    public string chatGroup;
    public string visitorGuid;
    public string type; //type = recoverChat
    public int campaignId;
    public bool isMobile;

    //其他字段
    public int chatVersion;
    public list<ChatMessage> chatMessages;

   ChatMessage 包括如下字段 (和getChatMessages 接口chat server 返回的字段内容一样):
   public class ChatMessage{
      public int type;
      public int id;
      public string guid;
      public long time;
      public string translated;
      ....
   }

```

#### 3.2 Site对应的chatserver实例恢复正常,聊天被切回

   1. 整个过程和场景1类似，在此不再复述.

### 3.3 OneMaxon

（**这部分等ARR的情况定了再细化**）

+ 为保证设计上的统一, 切换到Maxon服务器时的处理逻辑是一致的, chat server 端应该用同样的机制来判断是否需要恢复数据, 用公用的代码来恢复数据（否则代码要写2份，有问题的话要改2个地方）
+ 区别在于OneMaxon 是根据Moderator 返回的信息来确定要访问新的服务器, ARR 是请求直接被路由到新的服务器访客端或Agent Console 并不知道已经切换了服务器.

 **Question 1.分布式认证应该不包括Maxon的机器,切换到maxon的机器需要重新登录吗?**
 **所有状态都需要恢复吗? waif for chat, request chat 都需要吗?**