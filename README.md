DotNet_Logger
=========
Logging data with date to a log file


## Intension:
Easy and small project for saving log data into a file.

## How to use:
1. open singleton instance
2. call log functions

### Logging functions

You can log several different information:

1. Simple text messages:
	Use ‘Log(String msg)’ or ‘Log(LogPriority priority, Object sender, String msg)’ to write simple Text messages into the log-file.
Result in something like this: * 20.04.2016 09:57:42.532 	[DEBUG] 	(Oo.BasicObjects) 	renew XExtendedToolkit *

2. Messages for exceptions:	
To log exceptions you can use the functions ‘Log(Object sender, Exception e)’, ‘Log(LogPriority priority, Object sender, Exception e)’ or ‘Log(LogPriority priority, Object sender, String msg, Exception e)’. Here in addition to a text message the exception itself will be logged.

#### Parameters
**sender** - is the object/class that send the logging request. It can be a ‘String’ as well. If it is a ‘String’, the value is directly add as sender. If it is an ‘Object’, the class name of the object is set as sender
** priority** - is the priority of the message. If the priority is over the defined threshold, the message will not been saved in the file. The default priority for messages is ‘LogPriority.MIDDLE’. The threshold can be set by setting the public filed ‘Priority’ of the ‘Logger’ singleton instance.


### Log Priorities

If you don’t give a 

Priority | Value (as lower as more important!) | Description
------------ | ------------- | -------------
ALWAYS | 0 | Very important. Log should never happen.
IMPORTANT | 2 | Important. Log will not often happen, like light errors that could occur.
MIDDLE | 2 | Middle Priority. Log will happen regularly, such as process starts.
OFTEN | 6 | Unimportant. Log will happen often, like keyboard inputs and some events-calls.
DEBUG | 8 | Only for debug reasons. Log will happen very often, like checking loops and fast system events. This priority should not been logged in Release-Version or it should have a very good reason.




--	TODO: build a small workflow


## You want to know more?

--	TODO: build help from code doc

For getting a very detailed overview use the [code documentation section](/Help/index.html) of this project.

