using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ImageMagick;
using Microsoft.VisualBasic;

class Program
{
    public static bool isUpdate = false;
    public static string? TmpFilePath = null;
    public const string PSDTAG = "PSD";
    public const string LOCALTAG = "SELF";
    public static readonly string[] targetExts =[".jpg",".jpeg",".jpe",".png",".gif",".psd",".webp",".bmp",];
    public static readonly string[] targetChunks = [""];
    public static readonly string[] SEP = new string[]{","};
    public static readonly string[] EXCLUDE_TAGS = [PSDTAG,LOCALTAG,"score_9","score_8_up","score_7_up","masterpiece","hd","best quality","official art","official style","game cg","megami magazine","kyoto animation","very aesthetic","absurdres","perfect anatomy","8k","source_anime","ideal ratio body proportions","anime screencap","bad-artist-anime","negative_hand-neg","ribs","worst quality","low quality","(worst quality:1.5)","( low quality:1.5)","multiple breasts","(mutated hands and fingers)","(long body)","(mutation","poorly drawn)","black-white","bad anatomy","liquid body","liquid tongue","disfigured","malformed","mutated","anatomical nonsense","text font ui","error","malformed hands","long neck","blurred","lowers","lowres","bad anatomy","bad proportions","bad shadow","uncoordinated body","unnatural body","fused breasts","bad breasts","huge breasts","poorly drawn breasts","extra breasts","liquid breasts","heavy breasts","missing breasts","huge haunch","huge thighs","huge calf","bad hands","fused hand","missing hand","disappearing arms","disappearing thigh","disappearing calf","disappearing legs","fused ears","bad ears","poorly drawn ears","extra ears","liquid ears","heavy ears","missing ears","fused animal ears","bad animal ears","poorly drawn animal ears","extra animal ears","liquid animal ears","heavy animal ears","missing animal ears","error","missing fingers","missing limb","fused fingers","one hand with more than 5 fingers","one hand with less than 5 fingers","one hand with more than 5 digit","one hand with less than 5 digit","extra digit","fewer digits","fused digit","missing digit","bad digit","liquid digit","colorful tongue","black tongue","watermark","username","blurry","JPEG artifacts","signature","malformed feet","extra feet","bad feet","poorly drawn feet","fused feet","missing feet","extra shoes","bad shoes","fused shoes","more than two shoes","poorly drawn shoes","bad gloves","poorly drawn gloves","fused gloves","bad cum","poorly drawn cum","fused cum","bad hairs","poorly drawn hairs","fused hairs","big muscles","ugly","bad face","fused face","poorly drawn face","cloned face","big face","long face","bad eyes","fused eyes poorly drawn eyes","extra eyes","malformed limbs","more than 2 nipples","missing nipples","different nipples","fused nipples","bad nipples","poorly drawn nipples","black nipples","colorful nipples","gross proportions. short arm","(((missing arms)))","missing thighs","missing calf","missing legs","mutation","duplicate","morbid","mutilated","poorly drawn hands","more than 1 left hand","more than 1 right hand","deformed","(blurry)","disfigured","missing legs","extra arms","extra thighs","more than 2 thighs","extra calf","fused calf","extra legs","bad knee","extra knee","more than 2 legs","bad tails","bad mouth","fused mouth","poorly drawn mouth","bad tongue","tongue within mouth","too long tongue","black tongue","big mouth","cracked mouth","bad mouth","dirty face","dirty teeth","dirty pantie","fused pantie","poorly drawn pantie","fused cloth","poorly drawn cloth","bad pantie","yellow teeth","thick lips","bad cameltoe","colorful cameltoe","bad asshole","poorly drawn asshole","fused asshole","missing asshole","bad anus","bad pussy","bad crotch","bad crotch seam","fused anus","fused pussy","fused anus","fused crotch","poorly drawn crotch","fused seam","poorly drawn anus","poorly drawn pussy","poorly drawn crotch","poorly drawn crotch seam","bad thigh gap","missing thigh gap","fused thigh gap","liquid thigh gap","poorly drawn thigh gap","poorly drawn anus","bad collarbone","fused collarbone","missing collarbone","liquid collarbone","obesity","worst quality","low quality","normal quality","liquid tentacles","bad tentacles","poorly drawn tentacles","split tentacles","fused tentacles","missing clit","bad clit","fused clit","colorful clit","black clit","liquid clit","QR code","bar code","censored","safety panties","safety knickers","beard","mosaic","testis","censored"];
    public static readonly string[] EXCLUDE_PATTERNS = ["bad[ _].+",];
    public static string[] LOCAL_FNAME_PATTERNS = new string[]{@"^[0-9]{5}-[0-9]{10}\.[^.]{3,4}$",@"^.?[0-9]{5}\.[^.]{3,4}$"};

    static async Task Main(string[] args)
    {
        var startAt = DateTime.Now.ToString("yyyyMMddHHmmss");
        if (args.Length < 2)
        {
            Console.WriteLine(@"Usage: FileUploader <folderPath> <intervalInSeconds> [""update""] [""skipuntil"" <skio until this filename >] ");
            return;
        }

        string folderPath = args[0];
        //string[] targetFiles = Directory.GetFiles(folderPath).Where(a => targetExts.Contains(Path.GetExtension(a).ToLower())).ToArray();
        var di = new DirectoryInfo(folderPath);
        string[] targetFiles = di.GetFiles().Where(fi => targetExts.Contains(Path.GetExtension(fi.Name).ToLower()))
                                            .OrderBy(fi => fi.LastWriteTime)
                                            .Select(fi => fi.FullName)
                                            .ToArray();
        int intervalInSeconds = int.Parse(args[1]);
        string? skipUntil = null;
        if(args.Length > 2)
        {
            Regex regskip = new Regex("skipUntil",RegexOptions.IgnoreCase);
            Regex regUp = new Regex("update",RegexOptions.IgnoreCase);
            for(int i=0; i< args.Length;i++)
            {
                if (regskip.IsMatch(args[i]) &&  args.Length > i+1)
                    skipUntil = args[i+1];
                if (regUp.IsMatch(args[i]))
                    isUpdate = true;
            }
        }
        if(!string.IsNullOrWhiteSpace(skipUntil))
            Console.WriteLine($"skip until file={skipUntil}");
        if(isUpdate)
            Console.WriteLine("update mode.");
        string skipbuf = "";

        int fileCnt = 0;
        int totalCnt = targetFiles.Length;
        try
        {
            using (var cli = new HttpClient())
            {
                foreach (var filePath in targetFiles)
                {
                    if(!string.IsNullOrWhiteSpace(skipUntil))
                    {
                        if(!filePath.EndsWith(skipUntil))
                        {
                            skipbuf = ".";
                            Console.Write(skipbuf);
                            continue;
                        }
                        skipUntil = null;
                    }
                    if(isUpdate)
                    {
                        if(hasAlternateDataStream(filePath))
                        {
                            skipbuf = "_";
                            Console.Write(skipbuf);
                            continue;
                        }
                    }
                    if(!string.IsNullOrWhiteSpace(skipbuf))
                        Console.Write("\r\n");

                    await Task.Delay(intervalInSeconds * 1000);
                    string targetFilePath = filePath;
                    string fileExtension = Path.GetExtension(filePath).ToLower();
                    if (fileExtension != ".png" && fileExtension != ".jpg" && fileExtension != ".jpeg" && fileExtension != ".jpe")
                    {
                        targetFilePath = ConvertToPng(filePath);
                        if(targetFilePath is null)
                            continue;
                    }
                    var semdret = await UploadFile(cli, targetFilePath, filePath);
                    if(semdret != true)
                    {
                        Console.WriteLine($"[{fileCnt}/{totalCnt}] Err! {System.IO.Path.GetFileName(filePath)}");
                        using(var sw = new StreamWriter($@".\err_{startAt}.txt", true))
                        {
                            sw.WriteLine(filePath);
                        }
                        continue;
                    }
                    Console.WriteLine($"[{fileCnt}/{totalCnt}] ok   {System.IO.Path.GetFileName(filePath)}");
                    fileCnt++;
                }
            }
        }
        catch(Exception e)
        {
            Console.Write(e);
            deleteTmp();
        }
        finally
        {
            var efi = new FileInfo($@".\err_{startAt}.txt");
            if(efi.Exists)
            {
                using(var sr = new StreamReader($@".\err_{startAt}.txt"))
                {
                    Console.Write(sr.ReadToEnd());
                }
            }
        }
    }

    static string? ConvertToPng(string filePath)
    {
        try
        {
            if(TmpFilePath != null)
                deleteTmp();
            string tempFilePath = Path.GetTempFileName();
            TmpFilePath = Path.ChangeExtension(tempFilePath, ".png");

            using (var image = new MagickImage(filePath))
            {
                image.Write(TmpFilePath);
            }
            return TmpFilePath;
        }
        catch(Exception e)
        {
            Console.WriteLine(e);
            return null;
        }
    }

    static async Task<bool> UploadFile(HttpClient cli, string sendTargetFilePath, string originalFilePath)
    {
        string currentParameter = getAttribute(originalFilePath);

        string? responseString = null;
        using (var content = new MultipartFormDataContent())
        using (var fileStream = new FileStream(sendTargetFilePath, FileMode.Open, FileAccess.Read))
        {
            content.Add(new StreamContent(fileStream), "file", Path.GetFileName(sendTargetFilePath));

            var response = await cli.PostAsync("http://127.0.0.1:8019/tag-image/", content);
            if(!response.IsSuccessStatusCode)
                //throw new HttpRequestException(response.ReasonPhrase, null, response.StatusCode);
                return false;
            responseString = await response.Content.ReadAsStringAsync();

            //var jsonResponse = JsonDocument.Parse(responseString);
            //string? hogeValue = jsonResponse.RootElement.GetProperty("hoge").GetString();

        }
        var saveret = await SaveToFile(originalFilePath, mergeTags(stripdq(responseString), currentParameter));
        if(!saveret)
            return saveret;
        deleteTmp();
        return true;
    }

    static async Task<HttpResponseMessage?> postIt(HttpClient cli, string url, string  sendTargetFilePath)
    {
        HttpResponseMessage? ret = null;
        for(int cnt = 1; cnt < 5; cnt++)
        {
            using (var content = new MultipartFormDataContent())
            using (var fileStream = new FileStream(sendTargetFilePath, FileMode.Open, FileAccess.Read))
            {
                content.Add(new StreamContent(fileStream), "file", Path.GetFileName(sendTargetFilePath));
                ret = await cli.PostAsync("http://127.0.0.1:8019/tag-image/", content);
                if(ret.IsSuccessStatusCode)
                    return ret;
                System.Threading.Thread.Sleep(1000 * cnt * cnt);
            }
        }
        return ret;
    }


    static async Task<bool> SaveToFile(string filePath, string inData)
    {
        if(string.IsNullOrWhiteSpace(inData))
            return false;
        var data = inData.Replace(", ",";").Replace(" ,",";").Replace(",",";");
        var ext = Path.GetExtension(filePath).ToLower();
        if(ext == ".psd")
            data = $"{PSDTAG};{data}";

        var fname = System.IO.Path.GetFileName(filePath);
        bool isLocal = false;
        foreach(var p in LOCAL_FNAME_PATTERNS)
        {
            Regex reg = new Regex(p);
            if (reg.IsMatch(fname))
            {
                isLocal = true;
                break;
            }
        }
        if(isLocal)
            data = $"{LOCALTAG};{data}";

        if(ext == ".jpe" || ext == ".jpeg" || ext == ".jpg" || ext == ".png")
        {
            using (var image = new MagickImage(filePath))
            {
                try
                {
                    // EXIF情報
                    var tagBytes = Encoding.ASCII.GetBytes(data);

                    var profile = image.GetExifProfile();
                    if(profile is null)
                        profile = new ExifProfile();

                    var XPKeywords = profile.GetValue(ExifTag.XPKeywords)?.GetValue();
                    if(XPKeywords != null )
                    {
                        //Console.WriteLine($"XPKeywords={Encoding.UTF8.GetString((byte[])XPKeywords)}");
                    }
                    profile.SetValue(ExifTag.XPKeywords, tagBytes);
                    var XPKeywordsAfter = profile.GetValue(ExifTag.XPKeywords)?.GetValue();
                    //Console.WriteLine($"XPKeywordsAfter={Encoding.UTF8.GetString((byte[])XPKeywordsAfter)}");

                    var XPComment = profile.GetValue(ExifTag.XPKeywords)?.GetValue();
                    if(XPComment != null )
                    {
                        //Console.WriteLine($"XPComment={Encoding.UTF8.GetString((byte[])XPComment)}");
                    }
                    profile.SetValue(ExifTag.XPComment, tagBytes);
                    var XPCommentAfter = profile.GetValue(ExifTag.XPKeywords)?.GetValue();
                    //Console.WriteLine($"XPCommentAfter={Encoding.UTF8.GetString((byte[])XPCommentAfter)}");

                    //profile.SetValue(ExifTag.XPKeywords, tagBytes);
                    //profile.SetValue(ExifTag.XPComment,tagBytes);
                    image.SetProfile(profile);
                    //image.Write(filePath);
                    await image.WriteAsync(filePath);
                }
                catch(Exception e)
                {
                    Console.WriteLine(e);
                    return false;
                }
            }
        }
        return await SaveToAlternateDataStream(filePath, data);
    }

    static async Task<bool> SaveToAlternateDataStream(string filePath, string? data)
    {
        try
        {
            string adsPath = filePath + ":TAGS";
            using (var writer = new StreamWriter(adsPath))
            {
                await writer.WriteAsync(data);
            }
        }
        catch(Exception e)
        {
            Console.WriteLine(e);
            return false;
        }
        return true;
    }

    static bool hasAlternateDataStream(string filePath)
    {
        string adsPath = filePath + ":TAGS";
        var fi = new FileInfo(adsPath);
        return fi.Exists;
    }

    static async Task<List<string>> GetAlternateDataStream(string filePath)
    {
        string stringData = null;
        try
        {
            string adsPath = filePath + ":TAGS";
            using (var reader = new StreamReader(adsPath))
            {
                stringData = await reader.ReadToEndAsync();
            }
            if(!string.IsNullOrWhiteSpace(stringData))
                return stringData.Split(';',StringSplitOptions.RemoveEmptyEntries).Select(a => a.Trim()).ToList();
        }
        catch(Exception e)
        {
            Console.WriteLine(e);
        }
        return new List<string>();
    }

    static string stripdq(string? inStr)
    {
        if(string.IsNullOrWhiteSpace(inStr))
            return string.Empty;
        return inStr.Trim('"');
    }

    static string getAttribute(string imagePath, string attrName = "parameters")
    {
        using (var image = new MagickImage(imagePath))
        {
            var profile = image.AttributeNames.Where(name => name.StartsWith(attrName));
            if(profile is null)
                return string.Empty;
            var dataStr = image.GetAttribute(attrName);
            if(dataStr is null)
                return string.Empty;
            return stripdq(dataStr);
        }
    }

    static string mergeTags(string tags, string parameterStr)
    {
        var ret = new List<string>();
        var excludes = new List<string>();
        excludes.AddRange(EXCLUDE_TAGS);

        if(string.IsNullOrWhiteSpace(parameterStr))
            return tags;
        var fromParams = GetTagsFromParameters(parameterStr);
        var tagList = tags.Split(SEP, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        var notInTags = fromParams.Except(tagList);
        tagList.AddRange(notInTags);
        tagList = tagList.Select(a => a.Trim().Replace(" ", "_")).Distinct().ToList();
        foreach(var p in EXCLUDE_PATTERNS)
        {
            Regex regex = new Regex(p);
            foreach(var t in tagList)
            {
                if (regex.IsMatch(t))
                    excludes.Add(t);
            }
        }    
        return string.Join(",", tagList.Except(excludes));
    }

    static string[] GetTagsFromParameters(string parameterStr)
    {
        var splchars = new char[]{'<','>','(',')','{','}','[',']'};
        var parameterStrArray = parameterStr.Split(SEP, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToArray();
        int firstKVIndex = int.MaxValue;
        var parameters = new List<string>();
        for(int i=0; i< parameterStrArray.Length;i++)
        {
            var s = parameterStrArray[i];
            //Console.WriteLine(s);
            if(s.Contains(':'))
            {
                if(s.Any(a =>splchars.Contains(a)))
                {
                    parameters.Add(s);
                    continue;
                }
                firstKVIndex = i;
                break;
            }
            parameters.Add(s);
        }
        return parameters.ToArray();
    }

    static void deleteTmp()
    {
        // 一時ファイルを削除
        if (!string.IsNullOrWhiteSpace(TmpFilePath))
        {
            File.Delete(TmpFilePath);
            TmpFilePath = null;
        }
    }
}
