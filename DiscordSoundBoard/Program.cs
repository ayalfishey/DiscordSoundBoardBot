using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace DiscordSoundBoard
{
    class Program
    {
        static void Main(string[] args)
        {
            DriveAccess.getAuth();
            SBBot bot = new SBBot();
            
        }
    }
}
