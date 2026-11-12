using System.Security.Cryptography;
using System.Text;
using Npgsql;

internal static class DiscussionDemo
{
    private sealed record Topic(string Title,string Body,string TeacherReply,string StudentReply,string AssistantReply);

    private static Guid Id(Guid course,int thread,string kind,int index=0)
        => new(SHA256.HashData(Encoding.UTF8.GetBytes($"nsysu-discussion-demo-v1/{course:N}/{thread}/{kind}/{index}"))[..16]);

    private static Topic[] TopicsFor(string courseTitle)=>courseTitle switch
    {
        "個案分析課程－一三五"=>
        [
            new("個案事實與推論怎麼分開？","我整理個案紀錄時，會先把可直接觀察的行為、當事人原話與時間點列在「事實」欄，再把可能原因放在「推論」欄。若一句話同時包含兩者，大家會怎麼拆開？","可以先圈出能回到原始紀錄核對的部分；其餘即使合理，也要標成待驗證的假設。","我會在每項推論後面加上支持證據編號，沒有編號的先不寫進結論。","也請保留彼此衝突的紀錄，不要為了讓敘事順暢而刪掉例外。"),
            new("哪一項證據最能支持初步判斷？","目前有訪談摘要、事件時間軸與兩次現場觀察。我認為時間軸最能先確認事件順序，但還不足以解釋原因。大家會優先採用哪一項證據，理由是什麼？","判斷證據時可比較來源距離、可重複核對性及是否直接回答問題，不必只選一種。","我會用時間軸確定先後，再用訪談與觀察互相驗證；若不一致就標記出來。","請把『資料較完整』和『能支持特定結論』分開說明，兩者不一定相同。"),
            new("遺漏資料會如何影響分析？","個案在兩個關鍵時段沒有觀察紀錄，也缺少本人對事件的說明。我擔心目前的分析會高估旁觀者敘述。這些缺口應如何寫進報告？","把缺口列為分析限制，並說明它可能讓哪些結論變得不穩定；不要用推測補成事實。","我會把暫時結論改成條件句，並列出下一步要詢問的人與資料。","若無法補資料，也要交代目前能回答與不能回答的範圍。")
        ],
        "個案分析課程－二四六"=>
        [
            new("兩個方案應該用哪些標準比較？","我們正在比較立即介入與分階段介入兩個方案。我先列出可行性、預期效益、所需資源與執行風險，但各項重要性似乎不同。評估表要不要加權？","可以先說明評估目的，再決定權重；權重本身也需要理由，不能只為了讓偏好的方案勝出。","我建議先用同一組標準各自評分，再做一次不加權比較，看看結論是否一致。","也要加入當事人的接受度，否則技術上可行的方案未必能實施。"),
            new("利害關係人的影響如何整理？","同一方案對學生、教師、家長與行政人員的影響不同。我想用矩陣整理每一方的收益、負擔與可能疑慮，還需要加入哪些欄位？","可再加入影響發生的時間與可逆性，短期負擔和長期風險不應混在一起。","我會標記哪些意見來自訪談、哪些只是小組推測，避免把假設當成立場。","不要只列反對意見，也要記錄各方可接受的調整條件。"),
            new("方案風險與備案怎麼寫？","如果首選方案執行兩週後沒有改善，我們打算依觀察指標決定是否調整。備案應該寫到什麼程度，才不會只是空泛地說『再評估』？","請設定可觀察的觸發條件、負責人與下一步措施，讓備案能實際執行。","我們組會把風險分成發生機率與影響程度，再為高風險項目準備替代作法。","也要註明停止方案的條件，避免因投入成本而一直延續無效作法。")
        ],
        "數學課"=>
        [
            new("百分比換算時分母該怎麼選？","題目說某類人數從 40 人增加到 50 人。我算增加 10 人，但百分比應該除以原本的 40，還是新的 50？我想先確認基準量的判斷方式。","描述『增加百分比』時，基準通常是原來的 40 人，所以是 10÷40；寫答案時請把基準量一併標出。","我會先寫『相對於原人數』再列式，這樣比較不容易選錯分母。","可以再比較『50 人中有多少是新增』，它是另一個問題，分母才會是 50。"),
            new("平均數會不會掩蓋資料差異？","兩組成績平均都是 75 分，但第一組集中在 70 到 80，第二組從 40 到 100 都有。只報平均數看起來一樣，我們還應該比較什麼？","至少搭配全距或四分位距，並查看分布；平均相同不代表資料型態相同。","我把兩組資料畫成點圖後，第二組的離散程度明顯很多，比只看表格清楚。","若有極端值，也請說明它如何影響平均數，必要時可一起比較中位數。"),
            new("如何用估算快速檢查答案？","計算 198×4.9 時，我先估成 200×5≈1000，再檢查正式答案是否接近。大家還有哪些適合用估算抓錯的方法？","很好；估算的目的不是取代精確計算，而是檢查數量級、正負號與小數點位置。","做百分比題時，我會先判斷答案應小於還是大於原數，再看計算結果是否合理。","請在解題旁保留一句估算依據，讓檢查過程也能被閱讀。")
        ],
        "科學A"=>
        [
            new("控制變因與操作變因如何區分？","我們要測試光照時間是否影響幼苗高度，打算改變每天照光時數，固定水量、土壤與植物種類。這樣操作變因和控制變因的分類正確嗎？","正確；操作變因是你主動改變的光照時間，控制變因則盡量維持一致，應變變因是測得的幼苗高度。","我還會固定量測時間，否則早晚測量可能造成額外差異。","請先定義高度從哪裡量到哪裡，讓每次量測方法一致。"),
            new("為什麼實驗需要對照組？","如果所有幼苗都增加光照，我們雖然能看到生長變化，卻不知道是不是自然生長造成的。對照組應維持原本光照條件嗎？","是；對照組提供比較基準，除了操作變因外，其餘條件應和實驗組一致。","我想每組都放三盆幼苗，避免只靠單一植株的偶然結果。","重複樣本能降低個體差異影響，但仍需記錄未能控制的環境變化。"),
            new("測量誤差應該怎麼記錄？","同一株幼苗連續量三次得到 8.1、8.3、8.2 公分。我們應只寫平均值，還是保留每次讀值與量具精度？","原始讀值要保留，再報平均值與量具最小刻度；這樣讀者才能判斷變異程度。","我會由兩位組員各量一次，若差太多就檢查起點與視線是否一致。","不要把所有差異都稱為人為失誤，先區分量測不確定性與操作疏失。")
        ],
        "測試課程"=>
        [
            new("校園觀察要先訂哪些紀錄欄位？","我們準備觀察午休時圖書館入口的使用情況，先列了時間、進出人數、停留位置與事件描述。還需要記錄天氣或活動日等背景嗎？","背景條件會影響人流，建議記錄日期、時段、天氣與是否有特殊活動，之後比較才有依據。","我會把可計數資料和文字觀察分開，避免一邊數人一邊漏記事件。","也請避免記錄可識別個人的資訊，觀察重點放在空間與行為模式。"),
            new("同一地點不同時段如何比較？","早上、午休與放學的人流量差很多，直接比較總人數可能不公平。我們是否應統一每次觀察 15 分鐘，再換算每分鐘人數？","統一觀察長度是好方法，也要確保取樣位置與計數規則一致。","如果某時段遇到下雨，我會保留資料但加註，不和一般日直接合併。","可連續觀察多天，避免用單一天的結果代表整體情況。"),
            new("如何把觀察結果轉成改善建議？","我們發現入口尖峰時常出現逆向交錯，但目前只有三次觀察。報告可以直接建議改動線嗎，還是先提出小規模測試？","現有證據適合形成暫時假設；先提出可逆的小規模測試，並訂定改善前後的比較指標。","我們想用地貼引導單向通行一週，再比較尖峰通過時間與交錯次數。","建議也記錄使用者是否看得懂標示，不能只看流量數字。")
        ],
        "測試課程1"=>
        [
            new("哪種複習節奏比較容易維持？","我試過考前一次讀很久，也試過每天 25 分鐘分散複習。後者比較不累，但想知道要怎麼記錄成效，才不是只憑感覺？","可以固定使用同一組小測題，記錄正確率與隔天仍能回想的比例，再比較兩種節奏。","我會同時記實際完成次數，因為理論上有效但無法持續的方法不一定適合自己。","請把睡眠、作業量等明顯影響因素簡單註記，避免過度解釋單次結果。"),
            new("錯題整理要記錄哪些資訊？","目前我的錯題本只抄題目和正確答案，之後仍會犯同樣錯誤。是否應加入錯誤原因、修正方法與再測日期？","這三項都很重要；尤其要分辨是不懂概念、看錯條件，還是計算疏失。","我會用自己的話寫一條下次提醒，隔幾天遮住答案重新作答。","錯題本不必追求抄得完整，重點是能支持下一次判斷與練習。"),
            new("小組互助如何避免直接給答案？","同學卡住時，直接說答案雖然快，但他可能沒有理解。我們想改成先問他做到哪一步，再給一個提示，大家覺得可行嗎？","可行；提示應指向策略或條件，不要替對方完成關鍵推理，最後請他自己說明理由。","我們組會讓求助者先描述目前想法，其他人一次只提供一個提示。","也可在完成後互換角色，確認每個人都能獨立解釋解法。")
        ],
        _=>
        [
            new($"{courseTitle}：本單元最重要的概念是什麼？",$"整理「{courseTitle}」本單元內容時，我想先確認核心概念、支持證據與仍待釐清的問題。大家認為哪一點最重要？","請在提出重點時附上教材位置或可核對的依據。","我會把概念與例子配對整理，方便之後複習。","也請標記目前仍無法回答的問題。"),
            new($"{courseTitle}：如何檢查自己的理解？",$"完成「{courseTitle}」教材後，除了重讀之外，可以用哪些方式確認自己真的理解，而不是只覺得熟悉？","可嘗試不看教材解釋概念，再用新例子應用並回頭核對。","我會先寫下自己的答案，再和同學互相提問。","請把核對後修正的地方也留下來。"),
            new($"{courseTitle}：小組整理成果如何互相檢查？",$"我們想在繳交前交換檢查資料來源、推論與限制，避免每個人只看自己負責的部分。還有哪些檢查步驟？","可指定一位未參與該段撰寫的組員核對證據與結論是否相符。","我會另外檢查格式、引用與遺漏的反例。","整合時請保留不同意見與最後採用理由。")
        ]
    };

    internal static async Task Seed()
    {
        if(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")!="Development")
            throw new InvalidOperationException("Discussion demo requires Development.");
        var connection=Environment.GetEnvironmentVariable("ConnectionStrings__Portal");
        if(string.IsNullOrWhiteSpace(connection))throw new InvalidOperationException("Specify ConnectionStrings__Portal.");
        await using var db=NpgsqlDataSource.Create(connection);
        await using var conn=await db.OpenConnectionAsync();
        await using var tx=await conn.BeginTransactionAsync();
        await using(var gate=new NpgsqlCommand("SELECT pg_advisory_xact_lock(741026111)",conn,tx))await gate.ExecuteNonQueryAsync();

        var courses=new List<(Guid Id,string Title)>();
        await using(var cmd=new NpgsqlCommand("SELECT id,title FROM courses WHERE status='active' ORDER BY created_at,id",conn,tx))
        await using(var reader=await cmd.ExecuteReaderAsync())while(await reader.ReadAsync())courses.Add((reader.GetGuid(0),reader.GetString(1)));
        var insertedThreads=0;var insertedReplies=0;
        foreach(var course in courses)
        {
            var courseInsertedThreads=0;
            var members=new List<(Guid Id,string Role)>();
            await using(var cmd=new NpgsqlCommand("SELECT user_id,course_role FROM course_members WHERE course_id=$1 AND status='active' ORDER BY CASE course_role WHEN 'teacher' THEN 0 WHEN 'ta' THEN 1 ELSE 2 END,user_id",conn,tx))
            {cmd.Parameters.AddWithValue(course.Id);await using var reader=await cmd.ExecuteReaderAsync();while(await reader.ReadAsync())members.Add((reader.GetGuid(0),reader.GetString(1)));}
            var teachers=members.Where(x=>x.Role=="teacher").Select(x=>x.Id).ToArray();
            var assistants=members.Where(x=>x.Role=="ta").Select(x=>x.Id).ToArray();
            var students=members.Where(x=>x.Role=="student").Select(x=>x.Id).ToArray();
            if(teachers.Length==0 || students.Length==0)continue;
            var topics=TopicsFor(course.Title);
            for(var i=0;i<topics.Length;i++)
            {
                var thread=Id(course.Id,i,"thread");var author=students[i%students.Length];
                await using(var cmd=new NpgsqlCommand("INSERT INTO course_questions(id,course_id,author_id,title,question,locked,created_at) VALUES($1,$2,$3,$4,$5,$6,now()-make_interval(days=>$7)) ON CONFLICT DO NOTHING",conn,tx))
                {cmd.Parameters.AddWithValue(thread);cmd.Parameters.AddWithValue(course.Id);cmd.Parameters.AddWithValue(author);cmd.Parameters.AddWithValue(topics[i].Title);cmd.Parameters.AddWithValue(topics[i].Body);cmd.Parameters.AddWithValue(i==2);cmd.Parameters.AddWithValue(6-i*2);var added=await cmd.ExecuteNonQueryAsync();insertedThreads+=added;courseInsertedThreads+=added;}
                await using(var update=new NpgsqlCommand("UPDATE course_questions SET title=$3,question=$4 WHERE id=$1 AND course_id=$2",conn,tx))
                {update.Parameters.AddWithValue(thread);update.Parameters.AddWithValue(course.Id);update.Parameters.AddWithValue(topics[i].Title);update.Parameters.AddWithValue(topics[i].Body);await update.ExecuteNonQueryAsync();}
                var replies=new List<(Guid Author,string Body)>{
                    (teachers[0],topics[i].TeacherReply),
                    (students[(i+1)%students.Length],topics[i].StudentReply)
                };
                if(assistants.Length>0)replies.Add((assistants[0],topics[i].AssistantReply));
                for(var r=0;r<replies.Count;r++)
                {
                    var reply=Id(course.Id,i,"reply",r);
                    await using(var cmd=new NpgsqlCommand("INSERT INTO course_question_replies(id,question_id,author_id,body,created_at) VALUES($1,$2,$3,$4,now()-make_interval(days=>$5)+make_interval(hours=>$6)) ON CONFLICT DO NOTHING",conn,tx))
                    {cmd.Parameters.AddWithValue(reply);cmd.Parameters.AddWithValue(thread);cmd.Parameters.AddWithValue(replies[r].Author);cmd.Parameters.AddWithValue(replies[r].Body);cmd.Parameters.AddWithValue(5-i*2);cmd.Parameters.AddWithValue(r+1);insertedReplies+=await cmd.ExecuteNonQueryAsync();}
                    await using(var update=new NpgsqlCommand("UPDATE course_question_replies SET body=$3 WHERE id=$1 AND question_id=$2",conn,tx))
                    {update.Parameters.AddWithValue(reply);update.Parameters.AddWithValue(thread);update.Parameters.AddWithValue(replies[r].Body);await update.ExecuteNonQueryAsync();}
                    foreach(var liker in members.Select(x=>x.Id).Where(x=>x!=replies[r].Author).Take(1+(r%2)))
                    {await using var like=new NpgsqlCommand("INSERT INTO course_question_reply_likes(reply_id,user_id) VALUES($1,$2) ON CONFLICT DO NOTHING",conn,tx);like.Parameters.AddWithValue(reply);like.Parameters.AddWithValue(liker);await like.ExecuteNonQueryAsync();}
                }
                foreach(var voter in members.Select(x=>x.Id).Where(x=>x!=author).Take(2+i))
                {await using var vote=new NpgsqlCommand("INSERT INTO course_question_votes(question_id,user_id) VALUES($1,$2) ON CONFLICT DO NOTHING",conn,tx);vote.Parameters.AddWithValue(thread);vote.Parameters.AddWithValue(voter);await vote.ExecuteNonQueryAsync();}
            }
            if(courseInsertedThreads>0)
            {await using var audit=new NpgsqlCommand("INSERT INTO audit_events(actor_user_id,entity_type,entity_id,action,metadata) VALUES($1,'courses',$2,'demo.course_discussions.v1',jsonb_build_object('threads',3))",conn,tx);audit.Parameters.AddWithValue(teachers[0]);audit.Parameters.AddWithValue(course.Id);await audit.ExecuteNonQueryAsync();}
        }
        await tx.CommitAsync();
        Console.WriteLine($"Discussion demo seeded: {courses.Count} active courses, {insertedThreads} new threads, {insertedReplies} new replies.");
    }

    internal static async Task Audit()
    {
        if(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")!="Development")
            throw new InvalidOperationException("Discussion demo audit requires Development.");
        var connection=Environment.GetEnvironmentVariable("ConnectionStrings__Portal");
        if(string.IsNullOrWhiteSpace(connection))throw new InvalidOperationException("Specify ConnectionStrings__Portal.");
        await using var db=NpgsqlDataSource.Create(connection);
        await using var conn=await db.OpenConnectionAsync();
        var verified=0;
        var expectedTitles=new Dictionary<string,string[]>(StringComparer.Ordinal)
        {
            ["個案分析課程－一三五"]=["個案事實與推論怎麼分開？","哪一項證據最能支持初步判斷？","遺漏資料會如何影響分析？"],
            ["個案分析課程－二四六"]=["兩個方案應該用哪些標準比較？","利害關係人的影響如何整理？","方案風險與備案怎麼寫？"],
            ["數學課"]=["百分比換算時分母該怎麼選？","平均數會不會掩蓋資料差異？","如何用估算快速檢查答案？"],
            ["科學A"]=["控制變因與操作變因如何區分？","為什麼實驗需要對照組？","測量誤差應該怎麼記錄？"],
            ["測試課程"]=["校園觀察要先訂哪些紀錄欄位？","同一地點不同時段如何比較？","如何把觀察結果轉成改善建議？"],
            ["測試課程1"]=["哪種複習節奏比較容易維持？","錯題整理要記錄哪些資訊？","小組互助如何避免直接給答案？"]
        };
        await using var coursesCommand=new NpgsqlCommand("SELECT id,title FROM courses WHERE status='active' ORDER BY created_at,id",conn);
        var courses=new List<(Guid Id,string Title)>();
        await using(var reader=await coursesCommand.ExecuteReaderAsync())
            while(await reader.ReadAsync())courses.Add((reader.GetGuid(0),reader.GetString(1)));
        foreach(var course in courses)
        {
            await using var eligibility=new NpgsqlCommand("SELECT count(*) FILTER (WHERE course_role='teacher'),count(*) FILTER (WHERE course_role='student') FROM course_members WHERE course_id=$1 AND status='active'",conn);
            eligibility.Parameters.AddWithValue(course.Id);
            await using var eligibilityReader=await eligibility.ExecuteReaderAsync();
            await eligibilityReader.ReadAsync();
            var eligible=eligibilityReader.GetInt64(0)>0 && eligibilityReader.GetInt64(1)>0;
            await eligibilityReader.CloseAsync();
            if(!eligible)
            {
                Console.WriteLine($"SKIP {course.Title}: requires an active teacher and student.");
                continue;
            }
            var ids=Enumerable.Range(0,3).Select(i=>Id(course.Id,i,"thread")).ToArray();
            if(!expectedTitles.TryGetValue(course.Title,out var titles))
                throw new InvalidOperationException($"Discussion demo audit has no course-specific content for {course.Title}.");
            for(var i=0;i<ids.Length;i++)
            {
                await using var titleCheck=new NpgsqlCommand("SELECT title=$3 FROM course_questions WHERE course_id=$1 AND id=$2",conn);
                titleCheck.Parameters.AddWithValue(course.Id);titleCheck.Parameters.AddWithValue(ids[i]);titleCheck.Parameters.AddWithValue(titles[i]);
                if(await titleCheck.ExecuteScalarAsync() is not true)
                    throw new InvalidOperationException($"Discussion demo title is not course-specific for {course.Title}, thread {i+1}: expected '{titles[i]}'.");
            }
            const string sql="""
                SELECT count(*),
                       count(*) FILTER (WHERE locked),
                       (SELECT count(*) FROM course_question_replies WHERE question_id=ANY($1)),
                       (SELECT count(*) FROM course_question_votes WHERE question_id=ANY($1)),
                       (SELECT count(*) FROM course_question_reply_likes l JOIN course_question_replies r ON r.id=l.reply_id WHERE r.question_id=ANY($1))
                FROM course_questions
                WHERE course_id=$2 AND id=ANY($1)
                """;
            await using var check=new NpgsqlCommand(sql,conn);
            check.Parameters.AddWithValue(ids);
            check.Parameters.AddWithValue(course.Id);
            await using var result=await check.ExecuteReaderAsync();
            await result.ReadAsync();
            var threads=result.GetInt64(0);var locked=result.GetInt64(1);var replies=result.GetInt64(2);var votes=result.GetInt64(3);var likes=result.GetInt64(4);
            if(threads!=3 || locked!=1 || replies<6 || votes<9 || likes<6)
                throw new InvalidOperationException($"Discussion demo audit failed for {course.Title}: threads={threads}, locked={locked}, replies={replies}, votes={votes}, replyLikes={likes}.");
            Console.WriteLine($"OK {course.Title}: {threads} threads, {replies} replies, {votes} thread likes, {likes} reply likes, {locked} closed.");
            verified++;
        }
        Console.WriteLine($"Discussion demo audit passed: {verified} eligible active courses.");
    }
}
