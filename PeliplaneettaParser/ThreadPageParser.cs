using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AngleSharp.Dom;
using AngleSharp.Dom.Html;
using AngleSharp.Parser.Html;
using PeliplaneettaParser.Model;

namespace PeliplaneettaParser
{
    class ThreadPageParser
    {
        private readonly PeliplaneettaContext context;
        private readonly Dictionary<int, int> threadIdsPerOriginalThreadId;
        private readonly Dictionary<int, int> userIdsPerOriginalUserId;
        private readonly Dictionary<int, int> spaceIdsPerOriginalSpaceId;
        private readonly Dictionary<string, int> avatarIdsPerFilename;
        private readonly Dictionary<int, HashSet<int>> messagesPerOriginalSpaceId; 

        public ThreadPageParser(PeliplaneettaContext context)
        {
            this.context = context;

            threadIdsPerOriginalThreadId = context.Thread.AsNoTracking()
                .ToDictionary(t => t.OriginalThreadId, t => t.Id);

            userIdsPerOriginalUserId = context.User.AsNoTracking().ToDictionary(u => u.OriginalUserId, u => u.Id);

            spaceIdsPerOriginalSpaceId = context.Space.AsNoTracking().ToDictionary(s => s.OriginalSpaceId, s => s.Id);

            avatarIdsPerFilename = context.Avatar.AsNoTracking().ToDictionary(a => a.Filename, a => a.Id);

            messagesPerOriginalSpaceId = new Dictionary<int, HashSet<int>>();
        }

        public void Parse(string filePath)
        {
            string contents = File.ReadAllText(filePath, Encoding.GetEncoding("iso-8859-15"));

            HtmlParser parser = new HtmlParser();

            IHtmlDocument document = parser.Parse(contents);

            if (!IsThreadPage(document))
            {
                return;
            }

            string answerLink = GetAnswerLink(document);

            // If answer link is not found then with high probability site is broken or "Aihetta ei löytynyt valitsemaltasi alueelta"
            if (answerLink == null)
            {
                return;
            }

            int originalSpaceId = GetOriginalSpaceId(answerLink);
            int originalThreadId = GetOriginalThreadId(answerLink);

            int pageNumber = GetPageNumber(document);
            int pageCount = GetPageCount(document);

            int threadId;

            if (!threadIdsPerOriginalThreadId.TryGetValue(originalThreadId, out threadId))
            {
                threadId = CreateThread(document, originalThreadId, originalSpaceId, pageCount);
            }

            ParseMessages(document, pageNumber, threadId, originalSpaceId);
        }

        private bool IsThreadPage(IHtmlDocument document)
        {
            try
            {
                string titleText = document.QuerySelector("title").TextContent;

                string[] splittedTitle = titleText.Split(new[] {":: "}, StringSplitOptions.None);

                if (splittedTitle.Length != 4)
                {
                    return false;
                }

                if (string.IsNullOrWhiteSpace(splittedTitle.Last()))
                {
                    return false;
                }

                return true;
            }
            catch (Exception)
            {
                // Fallback for regonizing every broken site
                return false;
            }

        }

        private string GetAnswerLink(IHtmlDocument document)
        {
            // For ex. http://www.peliplaneetta.net/keskustelut/vastaa?fid=12&tid=88239

            string answerLink =
                document.QuerySelectorAll("a:has(img)")
                    .Where(a => a.HasAttribute("href") && a.GetAttribute("href").Contains("vastaa"))
                    .Select(a => a.GetAttribute("href"))
                    .FirstOrDefault();

            if (answerLink == null)
            {
                return null;
            }

            if (answerLink.Contains("&pid"))
            {
                int pidIndex = answerLink.IndexOf("&pid");
                answerLink = answerLink.Remove(pidIndex);
            }

            return answerLink;
        }

        private int GetPageNumber(IHtmlDocument document)
        {
            string currentPageNumberSpanText = document.QuerySelectorAll("span")
                .Where(s => s.HasAttribute("style") && s.GetAttribute("style").Contains("#CCCCCC;"))
                .Select(s => s.TextContent).FirstOrDefault();

            // There is only one page because span is not there
            if (currentPageNumberSpanText == null)
            {
                return 1;
            }

            try
            {
                return int.Parse(currentPageNumberSpanText);
            }
            catch (FormatException)
            {
                // For some reason there can be dot or slash after page number
                return int.Parse(currentPageNumberSpanText.Replace(".", "").Replace("/", ""));
            }
            
        }

        private int GetPageCount(IHtmlDocument document)
        {
            // For ex. "Sivuja [53]: ensimm�inen ... < 27  28  29  30  31  32  33 > ... viimeinen"

            string pageCountText = document.QuerySelectorAll("div.small")
                .Where(d => d.TextContent.Contains("Sivuja")).Select(d => d.TextContent).FirstOrDefault();

            // There is only one page because span is not there
            if (pageCountText == null)
            {
                return 1;
            }

            int pageCount = int.Parse(pageCountText.Split('[')[1].Split(']')[0]);

            return pageCount;
        }

        private int GetOriginalThreadId(string answerLink)
        {
            try
            {
                return int.Parse(answerLink.Split(new[] { "tid=" }, StringSplitOptions.None)[1]);
            }
            catch (FormatException)
            {
                // For some weird reason there can be dot after tid. For ex. http://www.peliplaneetta.net/keskustelut/vastaa?fid=10&tid=89775.
                return int.Parse(answerLink.Split(new[] { "tid=" }, StringSplitOptions.None)[1].Replace(".", ""));
            }
        }

        private int GetOriginalSpaceId(string answerLink)
        {
            return int.Parse(answerLink.Split(new[] { "fid=" }, StringSplitOptions.None)[1].Split('&')[0]);
        }

        private int CreateThread(IHtmlDocument document, int originalThreadId, int originalSpaceId, int pageCount)
        {
            string threadTitle = GetThreadTitle(document);
            bool isThreadLocked = IsThreadLocked(document);

            int spaceId;

            if (!spaceIdsPerOriginalSpaceId.TryGetValue(originalSpaceId, out spaceId))
            {
                throw new Exception("Space missing even it should not.");
            }

            Thread thread = new Thread
            {
                OriginalThreadId = originalThreadId,
                PageCount = pageCount,
                SpaceId = spaceId,
                Title = threadTitle,
                IsLocked = isThreadLocked
            };

            context.Thread.Add(thread);
            context.SaveChanges();

            threadIdsPerOriginalThreadId.Add(originalThreadId, thread.Id);

            context.Entry(thread).State = EntityState.Detached;

            return thread.Id;
        }

        private bool IsThreadLocked(IHtmlDocument document)
        {
            var elements =
                document.QuerySelectorAll("img")
                    .Where(i => i.HasAttribute("src") && i.GetAttribute("src").Contains("lockedreply"));

            if (elements.Any())
            {
                return true;
            }

            return false;
        }

        private string GetThreadTitle(IHtmlDocument document)
        {
            string titleText = document.QuerySelector("title").TextContent;

            return titleText.Split(new[] {":: "}, StringSplitOptions.None).Last();
        }

        private void ParseMessages(IHtmlDocument document, int pageNumber, int threadId, int originalSpaceId)
        {
            var messageElements = document.QuerySelectorAll("td.forumBorder tr");

            int indexCount = 0;

            List<Message> newMessages = new List<Message>();

            // First one is header row
            for (int i = 1; i < messageElements.Length; i += 3)
            {
                IElement messageTr;
                IElement contactTr;

                try
                {
                    messageTr = messageElements[i]; // Taking message and user info
                    contactTr = messageElements[i + 1]; // Taking second row for registered date
                }
                catch (ArgumentOutOfRangeException)
                {
                    // Not enough information to parse. Maybe file is corrupted?
                    indexCount++;
                    continue;
                }


                int originalUserId;

                try
                {
                    originalUserId = GetOriginalUserId(contactTr);
                }
                catch (Exception)
                {
                    // For some reason user id can't be parsed. Maybe person has been banned or deleted?
                    indexCount++;
                    continue;
                }

                int userId;

                if (!userIdsPerOriginalUserId.TryGetValue(originalUserId, out userId))
                {
                    userId = CreateUser(originalUserId, messageTr, contactTr);
                }

                HashSet<int> spaceMessages;
                if (!messagesPerOriginalSpaceId.TryGetValue(originalSpaceId, out spaceMessages))
                {
                    spaceMessages = new HashSet<int>();
                    messagesPerOriginalSpaceId.Add(originalSpaceId, spaceMessages);
                }

                int originalMessageId = GetOriginalMessageId(contactTr);

                if (spaceMessages.Contains(originalMessageId))
                {
                    indexCount++;
                    continue;
                }

                Message message = CreateMessage(messageTr, threadId, originalMessageId, pageNumber, userId, indexCount);
                newMessages.Add(message);

                spaceMessages.Add(originalMessageId);

                indexCount++;

            }

            context.Message.AddRange(newMessages);
            context.SaveChanges();

            newMessages.ForEach(m => context.Entry(m).State = EntityState.Detached );
        }

        private Message CreateMessage(IElement messageTr, int threadId, int originalMessageId, int pageNumber, int userId, int index)
        {
            DateTime creationDateTime = GetMessageCreationDateTime(messageTr);
            DateTime? editDateTime = DateTimeGetMessageEditTime(messageTr);
            string text = GetMessage(messageTr);
            int messageIndex = index+((pageNumber-1)*25); // Every page has 25 messages, pageNumber starts from one

            Message message = new Message
            {
                CreationDateTime = creationDateTime,
                EditDateTime = editDateTime,
                OriginalMessageId = originalMessageId,
                Page = pageNumber,
                Text = text,
                UserId = userId,
                ThreadId = threadId,
                MessageIndex = messageIndex
            };

            return message;
        }

        private int GetOriginalMessageId(IElement contactTr)
        {
            // For ex. http://www.peliplaneetta.net/keskustelut/vastaa?fid=12&tid=101985&pid=3954536&quote=1

            IElement answerElement = contactTr.QuerySelectorAll("a")
                .FirstOrDefault(a => a.HasAttribute("href") && a.GetAttribute("href").Contains("vastaa"));

            if (answerElement == null)
            {
                throw new Exception("Answer link is missing for some reason");
            }

            string messageId = answerElement.GetAttribute("href");
            messageId = messageId.Split(new[] {"pid="}, StringSplitOptions.None).Last();
            messageId = messageId.Replace("&quote=1", "");

            return int.Parse(messageId);
        }

        private string GetMessage(IElement messageTr)
        {
            IElement hrElement = messageTr.QuerySelector("hr");
            string messageHtml = hrElement.ParentElement.InnerHtml;

            messageHtml = messageHtml.Split(new[] { "<hr size=\"1\" style=\"color: #98AEC6\">\n    " }, StringSplitOptions.None).Last();
            messageHtml = messageHtml.Split(new[] { "\n<span class=\"medium\">" }, StringSplitOptions.None).First();
            messageHtml = messageHtml.Split(new[] { "<br><br><div class=\"medium\">" }, StringSplitOptions.None).First();
            messageHtml = messageHtml.Split(new[] { "<span class=\"medium\">" }, StringSplitOptions.None).First();
            messageHtml = messageHtml.Replace("<br>", "");
            messageHtml = messageHtml.Replace("<hr>", "");

            string messageBBCode = ConvertHtmlToBBCode(messageHtml);

            return messageBBCode;
        }

        private string ConvertHtmlToBBCode(string html)
        {
            html = Regex.Replace(html, @"<!--italic--><i>(.*?)<\/i><!--\/italic-->", "[i]$1[/i]", RegexOptions.Singleline);
            html = Regex.Replace(html, @"<!--bold--><b>(.*?)<\/b><!--\/bold-->", "[i]$1[/i]", RegexOptions.Singleline);
            html = Regex.Replace(html, @"<!--underline--><u>(.*?)<\/u><!--\/underline-->", "[i]$1[/i]", RegexOptions.Singleline);
            html = Regex.Replace(html, @"<!--code--><code>(.*?)<\/code><!--\/code-->", "[code]$1[/code]", RegexOptions.Singleline);
            html = Regex.Replace(html, "<!--spoiler--><font style=\"background: #D4D0C8; color: #D4D0C8;\">(.*?)<\\/font><!--\\/spoiler-->", "[s]$1[/s]", RegexOptions.Singleline);
            html = Regex.Replace(html, "<!--autolink--><a href=\"(.*?)\".*?<\\/a><!--\\/autolink-->", "[url]$1[/url]", RegexOptions.Singleline);
            html = Regex.Replace(html, "<!--url1--><a href=\"(.*?)\".*?<\\/a><!--\\/url1-->", "[url]$1[/url]", RegexOptions.Singleline);
            html = Regex.Replace(html, "<!--url2--><a href=\"(.*?)\".*?>(.*?)<\\/a><!--\\/url2-->", "[url=$1]$2[/url]", RegexOptions.Singleline);
            html = Regex.Replace(html, "<!--ftp--><a href=\"(.*?)\".*?<\\/a><!--\\/ftp-->", "[ftp]$1[/ftp]", RegexOptions.Singleline);
            html = Regex.Replace(html, "<!--image--><img src=\"(.*?)\".*?><!--\\/image-->", "[img]$1[/img]", RegexOptions.Singleline);
            html = Regex.Replace(html, "<!--email--><a href=\"mailto:(.*?)\">.*?<\\/a><!--\\/email-->", "[email]$1[/email]", RegexOptions.Singleline);
            html = Regex.Replace(html, "<!--automail--><a href=\"mailto:(.*?)\">.*?<\\/a><!--\\/automail-->", "[email]$1[/email]", RegexOptions.Singleline);
            html = Regex.Replace(html, "<!--red--><span class=\"forumRed\">(.*?)<\\/span><!--\\/red-->", "[color=red]$1[/color]", RegexOptions.Singleline);
            html = Regex.Replace(html, "<!--blue--><span class=\"forumBlue\">(.*?)<\\/span><!--\\/blue-->", "[color=blue]$1[/color]", RegexOptions.Singleline);
            html = Regex.Replace(html, "<!--green--><span class=\"forumGreen\">(.*?)<\\/span><!--\\/green-->", "[color=green]$1[/color]", RegexOptions.Singleline);
            html = Regex.Replace(html, "<!--black--><span class=\"forumBlack\">(.*?)<\\/span><!--\\/black-->", "[color=black]$1[/color]", RegexOptions.Singleline);
            html = Regex.Replace(html, "<img src=\"http:\\/\\/www\\.peliplaneetta\\.net\\/images\\/forum\\/emoticons\\/.*?\" alt=\"(.*?)\".*?>", "$1", RegexOptions.Singleline);

            html = html.Replace("<!--quote--><div class=\"forumQuote\">", "[quote]");
            html = html.Replace("</div><!--/quote-->", "[/quote]");

            return html;
        }

        private DateTime? DateTimeGetMessageEditTime(IElement messageTr)
        {
            IElement divElement =
                messageTr.QuerySelectorAll("div.medium")
                    .FirstOrDefault(d => d.TextContent.Contains("muokkasi tätä viestiä"));

            if (divElement == null)
            {
                return null;
            }

            string dateText = divElement.TextContent.Split(new[] {"viestiä "}, StringSplitOptions.None).Last();
            dateText = dateText.Replace("]", "");

            DateTime dateTime;

            try
            {
                // For ex. 08.02.2005 klo 23:48
                dateTime = DateTime.ParseExact(dateText, "dd.MM.yyyy klo H:mm", CultureInfo.InvariantCulture);
            }
            catch (FormatException)
            {
                // It's possible that message is corrupted somehow (for ex. it cuts off).
                return null;
            }


            return dateTime;
        }

        private DateTime GetMessageCreationDateTime(IElement messageTr)
        {
            IElement divElement =
                messageTr.QuerySelectorAll("div")
                    .FirstOrDefault(d =>
                        d.HasAttribute("style") &&
                        d.GetAttribute("style").Contains("position: relative; width: 450px; overflow: hidden;")).FirstElementChild;

            string dateTimeText = divElement.TextContent.Replace("\n    Kirjoitettu: ", "");
            dateTimeText = dateTimeText.Remove(0, dateTimeText.IndexOf(' '));

            string[] dateParts = dateTimeText.Split(' ');
            int day = int.Parse(dateParts[1].Remove(dateParts[1].IndexOf('.')));
            int year = int.Parse(dateParts[3]);
            int hour = int.Parse(dateParts[5].Split(':').First());
            int minute = int.Parse(dateParts[5].Split(':').Last());

            int month = 0;

            switch (dateParts[2].Remove(dateParts[2].Length - 2).ToLower())
            {
                case "tammikuu":
                    month = 1;
                    break;
                case "helmikuu":
                    month = 2;
                    break;
                case "maaliskuu":
                    month = 3;
                    break;
                case "huhtikuu":
                    month = 4;
                    break;
                case "toukokuu":
                    month = 5;
                    break;
                case "kesäkuu":
                    month = 6;
                    break;
                case "heinäkuu":
                    month = 7;
                    break;
                case "elokuu":
                    month = 8;
                    break;
                case "syyskuu":
                    month = 9;
                    break;
                case "lokakuu":
                    month = 10;
                    break;
                case "marraskuu":
                    month = 11;
                    break;
                case "joulukuu":
                    month = 12;
                    break;
            }

            return new DateTime(year, month, day, hour, minute, 0);
        }

        private int CreateUser(int originalUserId, IElement messageTr, IElement contactTr)
        {
            bool unknownUser = false;

            string nickName = GetNickName(messageTr);

            // Sometimes nick name and registeration fiels are empty. Maybe user has been deleted?
            if (string.IsNullOrWhiteSpace(nickName))
            {
                nickName = "Tuntematon";
                unknownUser = true;
            }

            int? avatarId;
            DateTime registeredDate;
            string signature = null;
            int roleId = 1;

            if (unknownUser)
            {
                avatarId = null;
                registeredDate = DateTime.MinValue;
            }
            else
            {
                avatarId = GetAvatar(messageTr);
                registeredDate = GetRegisteredDate(contactTr);

                signature = GetSignature(messageTr);
                if (signature != null)
                {
                    signature = ConvertHtmlToBBCode(signature);
                }

                roleId = GetRoleId(messageTr);
            }

            User user = new User()
            {
                AvatarId = avatarId,
                Nickname = nickName,
                OriginalUserId = originalUserId,
                RegisterationDate = registeredDate,
                RoleId = roleId,
                Signature = signature
            };

            context.User.Add(user);
            context.SaveChanges();

            userIdsPerOriginalUserId.Add(originalUserId, user.Id);

            context.Entry(user).State = EntityState.Detached;

            return user.Id;
        }

        private string GetSignature(IElement messageTr)
        {
            var spans = messageTr.QuerySelectorAll("span.medium");

            if (spans.Length == 1)
            {
                return null;
            }

            IElement signatureSpan = spans.Last();

            // TODO: Decode html?
            string signature = signatureSpan.InnerHtml.Replace("<br>________________<br>", "");
            signature = signature.Replace("<br>", "\n");

            if (string.IsNullOrWhiteSpace(signature))
            {
                return null;
            }

            return signature;
        }

        private DateTime GetRegisteredDate(IElement contactTr)
        {
            string registeredDivText =
                contactTr.QuerySelectorAll("div.small")
                    .Where(d => d.TextContent.Contains("Rekisteröitynyt"))
                    .Select(d => d.TextContent)
                    .FirstOrDefault();

            string dateString = registeredDivText.Split(new[] {"Rekisteröitynyt\n    "}, StringSplitOptions.None).Last();

            DateTime registeredDate;

            // Usually date is in this format
            if (DateTime.TryParseExact(dateString, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out registeredDate) == false)
            {
                // ...But if is not, then we can try another format as fall back
                DateTime.TryParseExact(dateString, "d.M.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out registeredDate);
            }

            return registeredDate;
        }

        private int? GetAvatar(IElement messageTr)
        {
            // For ex. http://www.peliplaneetta.net/images/icons/avatars/blank.gif
            string avatarLink = messageTr.QuerySelector("img").GetAttribute("src");

            string avatarName = avatarLink.Split('/').Last().Split('.').First();

            if (avatarName == "blank" || string.IsNullOrWhiteSpace(avatarName))
            {
                return null;
            }

            int avatarId;

            if (!avatarIdsPerFilename.TryGetValue(avatarName, out avatarId))
            {
                Avatar avatar = new Avatar
                {
                    Filename = avatarName
                };
                context.Avatar.Add(avatar);

                context.SaveChanges();

                avatarId = avatar.Id;
                avatarIdsPerFilename.Add(avatarName, avatarId);
            }

            return avatarId;
        }

        private string GetNickName(IElement messageTr)
        {
            return messageTr.QuerySelector("b").TextContent;
        }

        private int GetRoleId(IElement messageTr)
        {
            var roleElement = messageTr.QuerySelectorAll("span.medium").FirstOrDefault();

            if (roleElement == null)
            {
                throw new Exception("Role element missing for some reason");
            }

            switch (roleElement.TextContent)
            {
                case "Moderaattori":
                    return 2;
                case "VIP-käyttäjä":
                    return 3;
                default:
                    return 1;
            }
        }

        private int GetOriginalUserId(IElement contactTr)
        {
            // "uusiyksityisviesti?uid=13881"

            string privateMessageLink = contactTr.QuerySelectorAll("a")
                .Where(a => a.HasAttribute("href") && a.GetAttribute("href").Contains("uusiyksityisviesti"))
                .Select(a => a.GetAttribute("href"))
                .FirstOrDefault();

            if (privateMessageLink == null)
            {
                throw new Exception("Private link missing for some reason.");
            }

            return int.Parse(privateMessageLink.Split(new[] {"uid="}, StringSplitOptions.None)[1]);
        }
    }
}
