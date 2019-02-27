# 集成改成设计

## 总体思路

   1. 集成功能要支持Maxon
   2. 可以走消息队列的要走消息队列
  
## 1. Zendesk

### 1.1 现状

  Zendesk 集成一共有2个功能需要通过Chat Server来实现:

  1. 设置Zendesk 对象的URL。(如果能根据访客信息找到Zendesk对象,Agent Console 会在头部区域显示Zendesk 的图标, 点击图标后会在新页面打开对应Zendesk 对象的URL)
  2. 聊天结束时自动 和 Submit offline Message 时会自动生成Zendesk 对象。
  具体的调用路径是ChatServer -> LiveChatIntegration -> Zendesk API, LiveChatIntegration 是一个Webservice

### 1.2 修改方案

+ 设置Zendesk 对象的URL
原来的实现方式是chat request 的时候新起一个线程,在线程里获取对应的URL并设置`CurrentVisitor.ZendeskURl`。
现在要用新方式实现：在`ChatRequest` 这个Event 里调用WebService 并修改`CurrentVisitor.ZendeskURl`

+ 聊天结束 & OfflineMessage生成Zendesk 对象.
  新的实现方式:
  `ChatEnd` 这个event 的方式把相关信息写到MQ由消费程序来进行后续处理。

+ 支持Maxon

  要支持Maxon还需要做如下事情:

  1. LiveChatIntegration 这个项目要部署到副服务器。
  2. 同步回迁需要同步`t_livechat_integration` 表。  
  3. Zendesk 的配置信息要同步到副服务器。

## 2. GotoMeeting

### 2.1 修改方案

GotoMeeting 应该是支持Maxon的, 只需要服务器也部署ScreenSharing 服务即可(没有数据库表需要迁移, 账号密码是存在Agent Console 端的), 明天再确认一下.
GotoMeeting 没有功能需要走异步的消息队列.

## 3. Salsforce

### 3.1 现状

 手动模式目前看来不太合适用异步的消息队列来处理, 理由有几个
    1. 大量交互, 例如创建完Lead以后要立即连接Salesforce 把对应的Lead 对象查询出来并重新在界面上绑定一下。
      （创建完成后用户可能会继续Edit Lead，所以要知道刚刚创建完的Lead 的Id，假设用消息队列方式消费程序就必须把Lead Id 传给ChatServer, 消息队列的处理又是有延迟的，客户的等待时间就要长很多.而且整个处理逻辑也会复杂很多。
    2. 用户的体验会有很大的变化: 原来如果保存失败，界面上会立即显示错误信息。如果用消息队列的方式，Agent 根本不知道Salesforce 对象是否保存成功. 只要提交消息队列成功，界面上就显示成功了.
       假设有这么一个场景:客户在某个字段上填了一个非法值（有些时候必须把数据提交到Salesforce后才知道对应的值是非法的）, 按旧的模式，把创建对象的请求提交到Salesforce 以后会立即返回错误信息，客户知道自己哪个字段填了非法值以后，重新修改一下再次提交即可。
       如果用消息队列模式，只要用户填的字段通过了界面上的基本检查对应的信息就会被保存到消息队列,界面上就会提示保存成功(实际上可能根本没保存成功)。用户只有登录Salesforce系统才知道其实Salesforce对象是没有保存成功的。

 自动模式是在聊天结束以后自动保存Salesforce 对象的, 但是如果后台保存错误，Agent Console 会弹出错误信息, 如果不考虑这个场景，自动模式下是可以用消息队列的方式来保存Salesforce对象的(用户体验差了很多)

### 3.2 修改方案

+ 自动模式的Salesforce Object 可以通过Consumer 来延迟处理
+ 手动模式因为代码的改动量太大而且不是本次项目的重点, 暂时不改动,

+ 支持Maxon
  1. Recovery 的时候Salesforce 的相关对象都要进行恢复(Maxon 和状态恢复的时候要加上这部分代码)
  2. 副服务器要部署LiveChatIntegration
  3. 同步回迁要同步如下2个表: t_livechat_salesforceFieldConfig, t_livechat_SalesforceIntegration

## 4. ticket

### 4.1 修改方案

   1.Ticket 在调用接口的时候是需要拿到生成的Ticket Id的, 所以不能把要处理的内容发到消息队列里来处理。
   2.Maxon 的服务器是没有部署Ticket 系统的, 所以在副服务器上调用的是主服务器对应的ticket 系统的接口, 在Chat Server 要做一定的改动，如果是副服务器，要拼装一下ticket 的webservice 的url 确定调用了正确的web service.