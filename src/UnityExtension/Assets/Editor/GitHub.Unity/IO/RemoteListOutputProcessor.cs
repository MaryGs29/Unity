using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using GitHub.Unity.Logging;

namespace GitHub.Unity
{
    class RemoteListOutputProcessor : BaseOutputProcessor
    {
        private static readonly ILogger logger = Logger.GetLogger<RemoteListOutputProcessor>();

        private string currentName;
        private string currentUrl;
        private List<string> currentModes;

        public event Action<GitRemote> OnRemote;

        public RemoteListOutputProcessor()
        {
            Reset();
        }

        public override void LineReceived(string line)
        {
            base.LineReceived(line);

            if (OnRemote == null)
                return;

            //origin https://github.com/github/VisualStudio.git (fetch)

            if (line == null)
            {
                ReturnRemote();
                return;
            }

            var proc = new LineParser(line);
            var name = proc.ReadUntilWhitespace();
            proc.SkipWhitespace();

            var url = proc.ReadUntilWhitespace();
            proc.SkipWhitespace();

            proc.MoveNext();
            var mode = proc.ReadUntil(')');

            if (currentName == null)
            {
                currentName = name;
                currentUrl = url;
                currentModes.Add(mode);
            }
            else if (currentName == name)
            {
                currentModes.Add(mode);
            }
            else
            {
                ReturnRemote();

                currentName = name;
                currentUrl = url;
                currentModes.Add(mode);
            }
        }

        private void ReturnRemote()
        {
            Debug.Assert(OnRemote != null, "OnRemote != null");

            var modes = currentModes.Select(s => s.ToLowerInvariant()).ToArray();

            var isFetch = modes.Contains("fetch");
            var isPush = modes.Contains("push");

            GitRemoteFunction remoteFunction;
            if (isFetch && isPush)
            {
                remoteFunction = GitRemoteFunction.Both;
            }
            else if (isFetch)
            {
                remoteFunction = GitRemoteFunction.Fetch;
            }
            else if (isPush)
            {
                remoteFunction = GitRemoteFunction.Push;
            }
            else
            {
                remoteFunction = GitRemoteFunction.Unknown;
            }

            string host;
            string user = null;
            var proc = new LineParser(currentUrl);
            if (proc.Matches("http") || proc.Matches("https"))
            {
                proc.MoveToAfter(':');
                proc.MoveNext();
                proc.MoveNext();
                host = proc.ReadUntil('/');
            }
            else
            {
                //Assuming SSH here
                user = proc.ReadUntil('@');
                proc.MoveNext();
                host = proc.ReadUntil(':');

                currentUrl = currentUrl.Substring(user.Length + 1);
            }

            OnRemote(new GitRemote
            {
                Name = currentName,
                Host = host,
                URL = currentUrl,
                User = user,
                Function = remoteFunction
            });

            Reset();
        }

        private void Reset()
        {
            currentName = null;
            currentModes = new List<string>();
            currentUrl = null;
        }
    }
}