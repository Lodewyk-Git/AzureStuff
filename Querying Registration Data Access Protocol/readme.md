## Where this project comes from 

This project is possible due to a post on how to [Query Domains](https://techcommunity.microsoft.com/t5/microsoft-sentinel-blog/querying-whois-registration-data-access-protocol-rdap-with-azure/ba-p/2774502)(See the link for more). The Azure Function is the main part of the implementation. The porject here arrised from the question: "How to raise an alert when a user received an email from a newly registered domain?"

It ended with the possibility of knowing when contact has been made with a new domain.

The values are retrieved by Azure Sentinel, using MDE and other logging services, only a domain is required. 

Once the data is retrieved from Sentinel, a TLD lookup is done against https://data.iana.org/rdap/dns.json. If the TLD is found, the Lookup is done to the website using the link provided. The required information, in this case registration info, is then collected and finally pushed to a custom table in Microsoft Sentinel.

This was my first ever attempt at writing C#, so don't hesitate to call me out on the bad code. 

The code under this section is for an Azure Function, that's why the library calls may look unexpected.

I've removed the local settings, but it would be wise to use it for mass deployment, of course the script will have to be changed very slightly

### TimerTrigger - C<span>#</span>

The `TimerTrigger` makes it incredibly easy to have your functions executed on a schedule. This sample demonstrates a simple use case of calling your function every 5 minutes.

#### How it works

For a `TimerTrigger` to work, you provide a schedule in the form of a [cron expression](https://en.wikipedia.org/wiki/Cron#CRON_expression)(See the link for full details). A cron expression is a string with 6 separate expressions which represent a given schedule via patterns. The pattern we use to represent every 5 minutes is `0 */5 * * * *`. This, in plain text, means: "When seconds is equal to 0, minutes is divisible by 5, for any hour, day of the month, month, day of the week, or year".
