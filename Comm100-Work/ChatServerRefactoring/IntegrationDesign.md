# 集成改成设计

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

### 2.1 现状

GotoMeeting 应该是支持Maxon的(没有数据库表需要迁移, 账号密码是存在Agent Console 端的), 明天再确认一下.

## 3. Salsforce

### 3.1 现状

 手动模型的代码本身很复杂，一个聊天会涉及多个Agent, 如果按新的模式做改动, 开发和测试量比较大
 自动模式相对简单

### 3.2 修改方案

+ 自动模式的Salesforce Object 可以通过Consumer 来延迟处理
+ 手动模式因为代码的改动量太大而且不是本次项目的重点, 暂时不改动,

+ 支持Maxon
  1. Recovery 的时候Salesforce 的相关对象都要进行恢复
  2. 副服务器要部署LiveChatIntegration
  3. 同步回迁要同步如下2个表: t_livechat_salesforceFieldConfig, t_livechat_SalesforceIntegration