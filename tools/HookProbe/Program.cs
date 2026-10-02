using Witcher3Modifier;

try
{
    if(args.Length==1 && args[0]=="autoloot-recovery-offline")
    {
        Exception[] waiting=[new TimeoutException("装备查询正在等待游戏运行。"),
            new InvalidOperationException("未能确认玩家对象结构，本次操作未执行；请先载入存档。"),
            new InvalidOperationException("自动拾取期间场景已变化。"),
            new InvalidOperationException("装备请求尚未完成。")];
        Exception[] failures=[new InvalidOperationException("附近物品查询脚本不匹配。"),
            new InvalidOperationException("角色查询数组的释放入口未确认。"),
            new InvalidOperationException("容器库存类型未通过检查。"),new IOException("read failed")];
        if(waiting.Any(e=>!GameItemScheduler.IsAutoLootWait(e)) || failures.Any(GameItemScheduler.IsAutoLootWait))
            throw new Exception("Auto-loot readiness and validation errors were misclassified");
        Console.WriteLine("Auto-loot recovery: recorded timeout/scene/busy cases wait; code/type/read failures remain fatal; no game access");
    }
    else if(args.Length==2 && args[0]=="autoloot-code-offline")
    {
        using var document=System.Text.Json.JsonDocument.Parse(File.ReadAllText(args[1]));
        byte[] source=Convert.FromHexString(document.RootElement.GetProperty("Code").GetString()!);
        byte[] original=(byte[])source.Clone(),code=GameItemScheduler.BuildLootQueryCode(source);
        int[] changes=Enumerable.Range(0,source.Length).Where(i=>source[i]!=code[i]).ToArray();
        if(!source.SequenceEqual(original) || code.Length!=source.Length || changes.Length!=2 || changes.Any(i=>code[i]!=0))
            throw new Exception("Private query rewrite changed more than the two flag bytes");
        byte[] unknown=(byte[])source.Clone();unknown[changes[0]]=0x80;
        bool rejected=false;
        try {GameItemScheduler.BuildLootQueryCode(unknown);}catch(InvalidOperationException){rejected=true;}
        if(!rejected) throw new Exception("Unknown query bytecode was accepted");
        Console.WriteLine($"Private general-entity query: {source.Length} byte script, changes at {string.Join(',',changes)} only; source preserved and unknown pattern rejected; no game access");
    }
    else if(args.Length==1 && args[0]=="autoloot-take-wait")
    {
        for(int attempt=0;attempt<45;attempt++)
        {
            var result=GameItemScheduler.RunAutoLoot(false);
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(result));
            if(result.Ready && result.Containers>0)
            {
                Console.WriteLine("Take: "+System.Text.Json.JsonSerializer.Serialize(GameItemScheduler.RunAutoLoot(true)));
                Console.WriteLine("Recheck: "+System.Text.Json.JsonSerializer.Serialize(GameItemScheduler.RunAutoLoot(false)));
                break;
            }
            Thread.Sleep(1000);
        }
    }
    else if(args.Length==1 && args[0]=="autoloot-query-wait")
    {
        for(int attempt=0;attempt<30;attempt++)
        {
            var result=GameItemScheduler.RunAutoLoot(false);
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(result));
            if(result.Ready && result.Containers>0) break;
            Thread.Sleep(1000);
        }
    }
    else if(args.Length==1 && args[0]=="autoloot-query")
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(GameItemScheduler.RunAutoLoot(false)));
    else if(args.Length==1 && args[0]=="autoloot-take")
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(GameItemScheduler.RunAutoLoot(true)));
    else if(args.Length==1 && args[0]=="autoloot-metadata-read")
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(GameItemScheduler.ReadAutoLootMetadata()));
    else if(args.Length==1 && args[0]=="facts-entry-read")
        Console.WriteLine("Facts add/remove live prefixes validated: "+GameItemScheduler.ValidateDebugFacts());
    else if(args.Length==1 && args[0]=="equipment-slots-read")
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new {State=GameItemScheduler.ReadEquipmentSlots().Slots,Enabled=GameItemScheduler.ReadEquipmentLevelBypass()}));
    else if(args.Length==1 && args[0]=="swim-neutral-check")
    {
        var target=GameItemScheduler.ActorScriptTarget("SetAnimationSpeedMultiplier",2);
        var reset=GameItemScheduler.ActorScriptTarget("ResetAnimationSpeedMultiplier",1);
        const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static;
        var property=typeof(GameItemScheduler).GetMethod("RespecProperty",flags)!;
        var read=typeof(GameItemScheduler).GetMethod("ReadEnemyCauser",flags)!;
        bool ready=GameMoney.WithInventory(false,(handle,game,_,module)=>
        {
            var array=((long Address,long Type))property.Invoke(null,[handle,module,target.Player,"animationMultiplierCausers"])!;
            Console.WriteLine("Swimming-state only read: "+GameItemScheduler.UpdateSwimSpeed(false,1));
            return GameMoney.ReadInt32(handle,array.Address+8)==0 && GameMoney.ReadInt32(handle,game+0x160)==0;
        });
        Console.WriteLine("Player set/reset metadata verified");
        if(!ready) Console.WriteLine("Neutral write skipped: paused or existing animation source");
        else
        {
            int id=BitConverter.ToInt32(GameItemScheduler.InvokeActorScript(target.Game,target.Player,target.Function,target.Entry,target.Player,target.Reference,
                [..BitConverter.GetBytes(1f),..BitConverter.GetBytes(-1)]));
            if(id<0) throw new Exception("Neutral source rejected");
            try {if((float?)read.Invoke(null,[target.Player,id])!=1f) throw new Exception("Neutral source readback failed");}
            finally {GameItemScheduler.InvokeActorScript(reset.Game,reset.Player,reset.Function,reset.Entry,reset.Player,reset.Reference,BitConverter.GetBytes(id));}
            if(read.Invoke(null,[target.Player,id]) is not null) throw new Exception("Neutral source remains");
            Console.WriteLine("Player 1x source add/read/reset passed; no swimming speed changed");
        }
    }
    else if(args.Length==1 && args[0]=="cat-bytecode-read")
        Console.WriteLine(GameMoney.WithInventory(false,(handle,_,_,module)=>
        {
            long instruction=module+0x1CF707A,table=instruction+7+GameMoney.ReadInt32(handle,instruction+3);
            return System.Text.Json.JsonSerializer.Serialize(Enumerable.Range(0,32).Select(opcode=>
            {
                long target=GameMoney.ReadInt64(handle,table+opcode*8);
                return new {Opcode=opcode,Rva=target==0?0:target-module,Code=target==0?"":Convert.ToHexString(GameMoney.ReadBytes(handle,target,100))};
            }));
        }));
    else if(args.Length==1 && args[0]=="motion-paused-check")
    {
        GameMoney.WithInventory(false,(handle,game,_,_)=>
        {
            if(GameMoney.ReadInt32(handle,game+0x160)==0) throw new Exception("Game is running; multiplier check skipped");
            GameProcessPause.Run(handle,()=>
            {
                foreach(string feature in new[]{"moveSpeed","jumpHeight"})
                {
                    string before=System.Text.Json.JsonSerializer.Serialize(GameItemScheduler.ReadPlayerMotion(feature));
                    try
                    {
                        GameItemScheduler.SetPlayerMotion(feature,true,2);
                        string after=System.Text.Json.JsonSerializer.Serialize(GameItemScheduler.ReadPlayerMotion(feature));
                        using var beforeDoc=System.Text.Json.JsonDocument.Parse(before);using var afterDoc=System.Text.Json.JsonDocument.Parse(after);
                        var original=beforeDoc.RootElement.EnumerateArray().Select(item=>item.GetProperty("Value").GetSingle()).ToArray();
                        var actual=afterDoc.RootElement.EnumerateArray().Select(item=>item.GetProperty("Value").GetSingle()).ToArray();
                        if(actual.Where((value,index)=>Math.Abs(value-(feature=="moveSpeed"?original[index]*2:original[index]/2))>.0001f).Any()) throw new Exception("2x scaling readback differs");
                        Console.WriteLine(feature+" paused 2x readback="+after);
                    }
                    finally {GameItemScheduler.SetPlayerMotion(feature,false,1);}
                    if(before!=System.Text.Json.JsonSerializer.Serialize(GameItemScheduler.ReadPlayerMotion(feature))) throw new Exception("Original values not restored");
                    Console.WriteLine(feature+" original parameters restored before resuming threads");
                }
            });
            return true;
        },extraAccess:0x800);
    }
    else if(args.Length==1 && args[0]=="motion-neutral-check")
    {
        foreach(string feature in new[]{"moveSpeed","jumpHeight"})
        {
            string before=System.Text.Json.JsonSerializer.Serialize(GameItemScheduler.ReadPlayerMotion(feature));
            try
            {
                GameItemScheduler.SetPlayerMotion(feature,true,1);
                string after=System.Text.Json.JsonSerializer.Serialize(GameItemScheduler.ReadPlayerMotion(feature));
                if(before!=after) throw new Exception("Neutral multiplier changed parameters");
            }
            finally {GameItemScheduler.SetPlayerMotion(feature,false,1);}
            if(before!=System.Text.Json.JsonSerializer.Serialize(GameItemScheduler.ReadPlayerMotion(feature))) throw new Exception("Restore changed parameters");
            Console.WriteLine(feature+" 1x write/read/restore kept all original fields: "+before);
        }
    }
    else if(args.Length==1 && args[0]=="motion-fields-read")
        Console.WriteLine(GameMoney.WithInventory(false,(handle,game,_,module)=>
        {
            long player=GameMoney.ReadInt64(handle,GameMoney.ReadInt64(handle,game+0xFD60)+8);
            var fields=new List<(long Obj,int Class,int Name,int Type,int Offset,int Flags,string Raw)>();
            void Inspect(long obj)
            {
                if(obj==0) return;
                long cls=GameMoney.ReadInt64(handle,obj+0x18);
                for(int depth=0;cls!=0 && depth<16;depth++,cls=GameMoney.ReadInt64(handle,cls+0x10))
                {
                    int count=GameMoney.ReadInt32(handle,cls+0x38);
                    if(count is <0 or >4096) throw new Exception("Invalid property count");
                    long properties=GameMoney.ReadInt64(handle,cls+0x30);
                    for(int i=0;i<count;i++)
                    {
                        long property=GameMoney.ReadInt64(handle,properties+i*8),type=GameMoney.ReadInt64(handle,property+8);
                        int offset=GameMoney.ReadInt32(handle,property+0x20),flags=GameMoney.ReadInt32(handle,property+0x24);
                        long storage=(flags&0x20)!=0?GameMoney.ReadInt64(handle,obj+0x20):obj;
                        fields.Add((obj,GameMoney.ReadInt32(handle,cls+0x2C),GameMoney.ReadInt32(handle,property+0x10),GameMoney.ReadInt32(handle,type+0x2C),offset,flags,Convert.ToHexString(GameMoney.ReadBytes(handle,storage+offset,8))));
                    }
                }
            }
            Inspect(player);
            var propertyMethod=typeof(GameItemScheduler).GetMethod("RespecProperty",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!;
            var controller=((long Address,long Type))propertyMethod.Invoke(null,[handle,module,player,"defaultLocomotionController"])!;
            Inspect(GameMoney.ReadInt64(handle,GameMoney.ReadInt64(handle,controller.Address)+8));
            int count=GameMoney.ReadInt32(handle,player+0xF8);long array=GameMoney.ReadInt64(handle,player+0xF0);
            for(int i=0;i<count && i<128;i++) Inspect(GameMoney.ReadInt64(handle,array+i*8));
            var names=GameInventory.ReadNames(handle,module,fields.SelectMany(f=>new[]{f.Class,f.Name,f.Type}));
            var jump=fields.First(f=>names.GetValueOrDefault(f.Name)=="m_JumpParmsIdleS");
            var jumpProperty=((long Address,long Type))propertyMethod.Invoke(null,[handle,module,jump.Obj,"m_JumpParmsIdleS"])!;
            int memberCount=GameMoney.ReadInt32(handle,jumpProperty.Type+0x38);
            long members=GameMoney.ReadInt64(handle,jumpProperty.Type+0x30);
            for(int i=0;i<memberCount;i++)
            {
                long member=GameMoney.ReadInt64(handle,members+i*8);
                int offset=GameMoney.ReadInt32(handle,member+0x20);
                fields.Add((jumpProperty.Address,GameMoney.ReadInt32(handle,jumpProperty.Type+0x2C),GameMoney.ReadInt32(handle,member+0x10),0,offset,0,Convert.ToHexString(GameMoney.ReadBytes(handle,jumpProperty.Address+offset,8))));
            }
            var findField=typeof(GameItemScheduler).GetMethod("RespecFindField",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!;
            long verticalField=(long)findField.Invoke(null,[handle,module,jumpProperty.Type,"m_VerticalMovementS",false])!;
            long verticalType=GameMoney.ReadInt64(handle,verticalField+8),verticalAddress=jumpProperty.Address+GameMoney.ReadInt32(handle,verticalField+0x20);
            memberCount=GameMoney.ReadInt32(handle,verticalType+0x38);members=GameMoney.ReadInt64(handle,verticalType+0x30);
            for(int i=0;i<memberCount;i++)
            {
                long member=GameMoney.ReadInt64(handle,members+i*8);int offset=GameMoney.ReadInt32(handle,member+0x20);
                fields.Add((verticalAddress,GameMoney.ReadInt32(handle,verticalType+0x2C),GameMoney.ReadInt32(handle,member+0x10),0,offset,0,Convert.ToHexString(GameMoney.ReadBytes(handle,verticalAddress+offset,8))));
            }
            names=GameInventory.ReadNames(handle,module,fields.SelectMany(f=>new[]{f.Class,f.Name,f.Type}));
            return System.Text.Json.JsonSerializer.Serialize(fields.Select(f=>new {Object=$"0x{f.Obj:X}",Class=names.GetValueOrDefault(f.Class),Name=names.GetValueOrDefault(f.Name),Type=names.GetValueOrDefault(f.Type),f.Offset,f.Flags,f.Raw}));
        }));
    else if(args.Length==1 && args[0]=="respec-read")
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(GameItemScheduler.ReadRespecState()));
    else if(args.Length==1 && args[0]=="respec-engine-query")
        Console.WriteLine($"Mutation points={GameItemScheduler.ReadRespecMutationPoints()}; full engine frame, no reset");
    else if(args.Length==1 && args[0]=="fun-read")
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(GameItemScheduler.FunDiagnostics()));
    else if(args.Length==1 && args[0]=="fun-pause-read")
        Console.WriteLine(GameMoney.WithInventory(false,(handle,game,_,_)=>"Pause="+GameMoney.ReadInt32(handle,game+0x160)));
    else if(args.Length==1 && args[0]=="enemy-metadata-read")
        Console.WriteLine(GameMoney.WithInventory(false,(handle,game,_,module)=>
        {
            long player=GameMoney.ReadInt64(handle,GameMoney.ReadInt64(handle,game+0xFD60)+8);
            var find=typeof(GameItemScheduler).GetMethod("FindAuxiliaryFunction",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!;
            var output=new List<object>();
            foreach(var (name,count) in new[]{("GetNPCsAndPlayersInRange",4),("SetAnimationSpeedMultiplier",2),("ResetAnimationSpeedMultiplier",1)})
            {
                long fn=(long)find.Invoke(null,[handle,module,player,name,"CActor",count,false])!;
                long parameters=GameMoney.ReadInt64(handle,fn+0x28);
                var fields=new List<object>();
                for(int i=0;i<count;i++)
                {
                    long property=GameMoney.ReadInt64(handle,parameters+i*8),type=GameMoney.ReadInt64(handle,property+8);
                    var names=GameInventory.ReadNames(handle,module,[GameMoney.ReadInt32(handle,property+0x10),GameMoney.ReadInt32(handle,type+0x2C)]);
                    fields.Add(new {Name=names.GetValueOrDefault(GameMoney.ReadInt32(handle,property+0x10)),Type=names.GetValueOrDefault(GameMoney.ReadInt32(handle,type+0x2C)),Offset=GameMoney.ReadInt32(handle,property+0x20),Flags=GameMoney.ReadInt32(handle,property+0x24),Size=GameMoney.ReadInt32(handle,type+0x60)});
                }
                long ret=GameMoney.ReadInt64(handle,fn+0x20),retType=ret==0?0:GameMoney.ReadInt64(handle,ret+8);
                output.Add(new {name,Function=$"0x{fn:X}",Parameters=fields,ReturnType=$"0x{retType:X}",ReturnDestructor=retType==0?0:GameMoney.ReadInt64(handle,GameMoney.ReadInt64(handle,retType)+0x70)-module});
            }
            return System.Text.Json.JsonSerializer.Serialize(output);
        }));
    else if(args.Length==1 && args[0]=="enemy-name-read")
        Console.WriteLine(GameMoney.WithInventory(false,(handle,_,_,module)=>
        {
            int none=GameMoney.ReadInt32(handle,module+0x2393625+GameMoney.ReadInt32(handle,module+0x2393621));
            return "Native default name="+none+" "+System.Text.Json.JsonSerializer.Serialize(GameInventory.ReadNames(handle,module,[none]));
        }));
    else if(args.Length==1 && args[0]=="weather-list-read")
        Console.WriteLine(GameMoney.WithInventory(false,(handle,game,_,module)=>
        {
            long instruction=module+0x23936B9;
            long engine=GameMoney.ReadInt64(handle,instruction+7+GameMoney.ReadInt32(handle,instruction+3));
            long world=GameMoney.ReadInt64(handle,GameMoney.ReadInt64(handle,engine+0xF0)+8);
            Console.WriteLine("EngineIsGame="+(engine==game)+" flag="+GameMoney.ReadBytes(handle,engine+0xFD3A,1)[0]+" worldGetter="+(GameMoney.ReadInt64(handle,GameMoney.ReadInt64(handle,engine)+0x158)-module).ToString("X"));
            long scene=GameMoney.ReadInt64(handle,world+0x228),manager=GameMoney.ReadInt64(handle,scene+0x92270);
            int count=GameMoney.ReadInt32(handle,manager+0x10);
            if(count is <1 or >1000) throw new Exception("Weather list layout not confirmed");
            long array=GameMoney.ReadInt64(handle,manager+8);
            var ids=Enumerable.Range(0,count).Select(i=>GameMoney.ReadInt32(handle,array+i*0x110)).ToArray();
            return System.Text.Json.JsonSerializer.Serialize(new {Count=count,World=$"0x{world:X}",Scene=$"0x{scene:X}",Manager=$"0x{manager:X}",Names=GameInventory.ReadNames(handle,module,ids)});
        }));
    else if(args.Length==1 && args[0]=="fun-weather-read")
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(GameItemScheduler.ReadFunWeather()));
    else if(args.Length==3 && args[0]=="gear-reduction-preview")
    {
        var item=GameInventory.Read().Single(item=>item.UniqueId==int.Parse(args[1]));
        string field=item.Category is "steelsword" or "silversword"?"damage":"armor";
        decimal target=decimal.Parse(args[2],System.Globalization.CultureInfo.InvariantCulture);
        decimal nearest=GameItemScheduler.RunForItem(item,()=>GameEquipmentNumbers.NearestTarget(item.UniqueId,item.Category,field,target));
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new {item.Name,item.UniqueId,Target=target,Nearest=nearest}));
    }
    else if(args.Length is 1 or 2 && args[0]=="reduction-runtime-test")
    {
        string name=args.Length==2?args[1]:"W_Axe05";
        if(name is not ("W_Axe05" or "Bear Armor" or "Viper School silver sword")) throw new Exception("Unknown dedicated test base");
        int id=GameEquipment.Create(name,[],"",0);
        var item=GameInventory.Read().Single(item=>item.UniqueId==id && item.Name==name);
        try
        {
            GameItemScheduler.RunForItem(item,()=>
            {
                string stem=item.Category=="armor"?"armor":item.Category=="silversword"?"silver":"steel";
                string field=item.Category=="armor"?"armor":"damage";
                GameItemScheduler.AddEquipmentAbility(id,"autogen_"+stem+"_base",true);
                for(int i=0;i<6;i++) GameItemScheduler.AddEquipmentAbility(id,"autogen_"+stem+(stem=="armor"?"_armor":"_dmg"),true);
                string[] unrelated=["weight","quality","armor_reduction","critical_hit_damage_bonus","dismember_chance"];
                var beforeOther=unrelated.ToDictionary(attribute=>attribute,attribute=>GameItemScheduler.ReadEquipmentAttributeParts(id,attribute));
                decimal before=GameEquipmentNumbers.Read(id,item.Category)[field];
                decimal target=GameEquipmentNumbers.NearestTarget(id,item.Category,field,before-9);
                if(target>=before-8) throw new Exception("Expected materially lower target");
                var applied=GameEquipmentNumbers.Apply(id,item.Category,new Dictionary<string,decimal>{{field,target}});
                foreach(string attribute in unrelated)
                    if(GameItemScheduler.ReadEquipmentAttributeParts(id,attribute)!=beforeOther[attribute]) throw new Exception("Unrelated attribute changed: "+attribute);
                var after=GameInventory.Read().Single(value=>value.UniqueId==id);
                if(after.Name!=item.Name || after.Category!=item.Category) throw new Exception("Item identity changed");
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new {TestItem=id,Name=name,Before=before,Requested=before-9,Target=target,Actual=applied[field],OtherAttributesUnchanged=unrelated}));
                return true;
            });
        }
        finally {Console.WriteLine("Deleted own test item: "+GameInventory.Delete(item,1));}
    }
    else if(args.Length==1 && args[0]=="reduction-selftest")
    {
        var lower=GameEquipmentNumbers.PlanBaseAdjustment(142.56775m,133.56775m,[7,8,7.829m,24.145m],[0,0,6,1]);
        if(lower.Target>=142.56775m || lower.Plan.Remove.Sum()==0) throw new Exception("Native generated increments were not reduced");
        decimal actual=142.56775m+new decimal[]{7,8,7.829m,24.145m}.Select((unit,i)=>unit*(lower.Plan.Add[i]-lower.Plan.Remove[i])).Sum();
        if(actual!=lower.Target) throw new Exception("Reduction plan does not sum to its target");
        var armor=GameEquipmentNumbers.PlanBaseAdjustment(135,94,[1,5,25],[0,20,1]);
        if(armor.Target!=94) throw new Exception("Armor exact reduction failed");
        var identity=GameEquipmentNumbers.PlanBaseAdjustment(94,94,[1,5,25],[0,20,1]);
        if(identity.Plan.Add.Sum()!=0 || identity.Plan.Remove.Sum()!=0) throw new Exception("Unchanged target wrote abilities");
        Console.WriteLine("Native generated increment reduction, exact armor target, decimal conservation and unchanged target passed");
    }
    else if(args.Length==2 && args[0]=="gear-base-read")
        Console.WriteLine(GameMoney.WithInventory(false,(handle,_,inventory,module)=>
        {
            int id=int.Parse(args[1]),count=GameMoney.ReadInt32(handle,inventory+0x148);
            long items=GameMoney.ReadInt64(handle,inventory+0x140);
            if(count is <0 or >10000) throw new Exception("Invalid inventory count");
            for(int i=0;i<count;i++)
            {
                long item=items+i*0x98;
                if(GameMoney.ReadInt32(handle,item+0x60)!=id) continue;
                long data=GameMoney.ReadInt64(handle,item+0xC);int n=GameMoney.ReadInt32(handle,item+0x14);
                if(n is <0 or >1000) throw new Exception("Invalid base ability count");
                int[] ids=Enumerable.Range(0,n).Select(j=>GameMoney.ReadInt32(handle,data+j*4)).ToArray();
                return System.Text.Json.JsonSerializer.Serialize(new {Id=id,Count=n,Seed=BitConverter.ToUInt16(GameMoney.ReadBytes(handle,item+0x68,2)),Header=Convert.ToHexString(GameMoney.ReadBytes(handle,item+0x50,0x48)),Abilities=GameInventory.ReadNames(handle,module,ids)});
            }
            throw new Exception("Item missing");
        }));
    else if(args.Length==2 && args[0]=="gear-ability-records")
        Console.WriteLine(GameMoney.WithInventory(false,(handle,game,_,module)=>
        {
            int id=GameItemScheduler.ResolveEquipmentAbility(args[1]);
            long defs=GameMoney.ReadInt64(handle,game+0xFD68);int bucketsCount=GameMoney.ReadInt32(handle,defs+0x60);
            long buckets=GameMoney.ReadInt64(handle,defs+0x80),node=GameMoney.ReadInt64(handle,buckets+(uint)id%(uint)bucketsCount*8);
            for(int i=0;node!=0 && i<10000;i++,node=GameMoney.ReadInt64(handle,node+0x68))
            {
                if(GameMoney.ReadInt32(handle,node)!=id) continue;
                int count=GameMoney.ReadInt32(handle,node+0x10);long records=GameMoney.ReadInt64(handle,node+8);
                if(count is <0 or >100) throw new Exception("Bad record count");
                var output=new List<object>();
                for(int j=0;j<count;j++)
                {
                    byte[] b=GameMoney.ReadBytes(handle,records+j*24,24);int attr=BitConverter.ToInt32(b);
                    output.Add(new {Attribute=GameInventory.ReadNames(handle,module,[attr]).GetValueOrDefault(attr),Mode=BitConverter.ToInt32(b,4),Random=b[8],First=BitConverter.ToSingle(b,12),Second=BitConverter.ToSingle(b,16),Round=unchecked((sbyte)b[20]),Hex=Convert.ToHexString(b)});
                }
                return System.Text.Json.JsonSerializer.Serialize(output);
            }
            throw new Exception("Missing definition");
        }));
    else if(args.Length is 1 or 2 && args[0]=="enemy-query-probe")
    {
        GameItemScheduler.RunSerialized(()=>
        {
            var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static;
            int queryFlags=args.Length==2 && args[1]=="all-alive"?0x5:0x105;
            object result=typeof(GameItemScheduler).GetMethod("QueryEnemyArray",flags)!.Invoke(null,[queryFlags])!;
            object Read(string name)=>result.GetType().GetProperty(name)!.GetValue(result)!;
            try {Console.WriteLine($"Nearby actor references (flags {queryFlags:X})="+((Dictionary<long,long>)Read("Actors")).Count);}
            finally {GameItemScheduler.ReleaseActorArray((long)Read("Game"),(long)Read("Player"),(long)Read("Type"),(byte[])Read("Header"));}
            Console.WriteLine("Query result released by native array destructor; no actor speed changed");
            return true;
        });
    }
    else if(args.Length==1 && args[0]=="actor-speed-roundtrip")
    {
        GameItemScheduler.RunSerialized(()=>
        {
            var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static;
            object result=typeof(GameItemScheduler).GetMethod("QueryEnemyArray",flags)!.Invoke(null,[0x5])!;
            object Read(string name)=>result.GetType().GetProperty(name)!.GetValue(result)!;
            var read=typeof(GameItemScheduler).GetMethod("ReadEnemyCauser",flags)!;
            try
            {
                var actors=(Dictionary<long,long>)Read("Actors");
                if(actors.Count==0) throw new Exception("No nearby non-player actor for roundtrip");
                var actor=actors.First();
                var target=GameItemScheduler.ActorScriptTarget("SetAnimationSpeedMultiplier",2,actor.Key);
                if(target.Reference!=actor.Value) throw new Exception("Actor changed");
                int id=BitConverter.ToInt32(GameItemScheduler.InvokeActorScript(target.Game,target.Player,target.Function,target.Entry,actor.Key,actor.Value,[..BitConverter.GetBytes(.5f),..BitConverter.GetBytes(-1)]));
                if(id<0) throw new Exception("Multiplier source rejected");
                try
                {
                    if((float?)read.Invoke(null,[actor.Key,id])!=.5f) throw new Exception("Half speed readback mismatch");
                    Console.WriteLine("Non-player actor multiplier 0.5 confirmed; own source="+id);
                }
                finally
                {
                    var reset=GameItemScheduler.ActorScriptTarget("ResetAnimationSpeedMultiplier",1,actor.Key);
                    GameItemScheduler.InvokeActorScript(reset.Game,reset.Player,reset.Function,reset.Entry,actor.Key,actor.Value,BitConverter.GetBytes(id));
                    if(read.Invoke(null,[actor.Key,id]) is not null) throw new Exception("Half speed source cleanup mismatch");
                    Console.WriteLine("Own source removed; original sources preserved");
                }
            }
            finally {GameItemScheduler.ReleaseActorArray((long)Read("Game"),(long)Read("Player"),(long)Read("Type"),(byte[])Read("Header"));}
            return true;
        });
    }
    else if(args.Length==1 && args[0]=="fun-time-roundtrip")
    {
        int before=GameItemScheduler.ReadFunTime();
        int minuteOfDay=before%86400/60;
        int target=minuteOfDay==1439?minuteOfDay-1:minuteOfDay+1;
        try {Console.WriteLine("Before="+before+" temporary="+GameItemScheduler.SetFunTime(target/60,target%60));}
        finally {Console.WriteLine("Restored="+GameItemScheduler.SetFunTime(minuteOfDay/60,minuteOfDay%60));}
    }
    else if(args.Length==1 && args[0]=="actor-parameter-query")
    {
        GameItemScheduler.RunSerialized(()=>
        {
            var game=GameMoney.WithInventory(false,(_,game,_,_)=>game);
            var target=GameItemScheduler.ActorScriptTarget("GetTimescalePriority",1,game,"CR4Game");
            int priority=BitConverter.ToInt32(GameItemScheduler.InvokeActorScript(target.Game,target.Player,target.Function,target.Entry,game,target.Reference,BitConverter.GetBytes(7)));
            if(priority!=20) throw new Exception("Typed parameter query returned "+priority);
            Console.WriteLine("Full engine invocation with one typed parameter returned CFM_On priority 20; no gameplay change");
            return true;
        });
    }
    else if(args.Length==1 && args[0]=="actor-speed-neutral-probe")
    {
        GameItemScheduler.RunSerialized(()=>
        {
            var target=GameItemScheduler.ActorScriptTarget("SetAnimationSpeedMultiplier",2);
            int id=BitConverter.ToInt32(GameItemScheduler.InvokeActorScript(target.Game,target.Player,target.Function,target.Entry,target.Player,target.Reference,[..BitConverter.GetBytes(1f),..BitConverter.GetBytes(-1)]));
            if(id<0) throw new Exception("Neutral animation source rejected");
            var read=typeof(GameItemScheduler).GetMethod("ReadEnemyCauser",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static)!;
            try
            {
                float? actual=(float?)read.Invoke(null,[target.Player,id]);
                if(actual!=1f) throw new Exception("Neutral causer readback mismatch");
                Console.WriteLine("Own 1.0 multiplier source ID="+id+" readback=1; total animation speed unchanged");
            }
            finally
            {
                var reset=GameItemScheduler.ActorScriptTarget("ResetAnimationSpeedMultiplier",1);
                if(reset.Player!=target.Player) throw new Exception("Player changed before neutral cleanup");
                GameItemScheduler.InvokeActorScript(reset.Game,reset.Player,reset.Function,reset.Entry,reset.Player,reset.Reference,BitConverter.GetBytes(id));
                if(read.Invoke(null,[target.Player,id]) is not null) throw new Exception("Neutral causer cleanup mismatch");
                Console.WriteLine("Own source removed; existing sources preserved");
            }
            return true;
        });
    }
    else if(args.Length==3 && args[0]=="enemy-speed")
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(GameItemScheduler.UpdateEnemySpeed(bool.Parse(args[1]),float.Parse(args[2],System.Globalization.CultureInfo.InvariantCulture))));
    else if(args.Length==3 && args[0]=="fun-time")
        Console.WriteLine("Time="+GameItemScheduler.SetFunTime(int.Parse(args[1]),int.Parse(args[2])));
    else if(args.Length==2 && args[0]=="fun-weather")
    {GameItemScheduler.SetFunWeather(args[1]);Console.WriteLine("Weather request accepted");}
    else if(args.Length==2 && args[0]=="fun-slow")
        Console.WriteLine("Actual time scale="+GameItemScheduler.SetFunSlow(float.Parse(args[1],System.Globalization.CultureInfo.InvariantCulture)));
    else if(args.Length==2 && args[0]=="fun-cat")
    {GameItemScheduler.SetFunCat(bool.Parse(args[1]));Console.WriteLine("Cat request executed");}
    else if(args.Length==2 && args[0]=="fun-beard")
        Console.WriteLine("Beard="+GameItemScheduler.SetFunBeard(int.Parse(args[1])));
    else if(args.Length==2 && args[0]=="fun-hair")
    {GameItemScheduler.SetFunHair(int.Parse(args[1]));Console.WriteLine("Hair mount confirmed");}
    else if(args.Length==1 && args[0]=="fun-hair-read")
        foreach(var item in GameInventory.Read().Where(item=>item.Category=="hair"))
            Console.WriteLine(item.Name+" id="+item.UniqueId+" mounted="+GameItemScheduler.IsItemMounted(item.UniqueId));
    else if(args.Length==1 && args[0]=="fun-selftest")
    {
        if(GameItemScheduler.FunTimeTarget(2*86400+80000,6,15)!=2*86400+6*3600+15*60) throw new Exception("Time day preservation failed");
        foreach(var pair in new[]{(-1,0),(24,0),(12,60)})
        {
            bool rejected=false;
            try {GameItemScheduler.FunTimeTarget(0,pair.Item1,pair.Item2);} catch(ArgumentOutOfRangeException) {rejected=true;}
            if(!rejected) throw new Exception("Invalid clock accepted");
        }
        Console.WriteLine("Time day preservation and hour/minute bounds passed; no game access");
    }
    else if(args.Length==1 && args[0]=="version-selftest")
    {
        GameVersion.Select("C272B2C2E61F84C758E28FAB69AB2915944DD1E539DBB435FAE9FC67494C7E25");
        if(GameVersion.Rva(0x26F93D2)!=0x26F93D2) throw new Exception("Legacy address mismatch");
        GameVersion.Select("9406ECCC12B68E08920931442EF6A57340E910D3E01F2082E88232487433FE51");
        if(GameVersion.Rva(0x26F93D2)!=0x26F4752 || GameVersion.Rva(0x5A750E8)!=0x5A4E0A8)
            throw new Exception("Current address mismatch");
        bool rejected=false;
        try { GameVersion.Rva(0x123456); } catch(InvalidOperationException) { rejected=true; }
        if(!rejected) throw new Exception("Unmapped current entry accepted");
        GameVersion.Select("different executable hash");
        if(GameVersion.Current500c || GameVersion.Rva(0x26F93D2)!=0x26F93D2)
            throw new Exception("Fallback profile mismatch");
        Console.WriteLine("Legacy/current address selection, unmapped entry guard and no blanket hash rejection passed; no game access");
    }
    else if(args.Length==1 && args[0]=="version-read")
    {
        Console.WriteLine($"money={GameMoney.Read()}, weight={GameWeight.Read()}, inventory={GameInventory.Read().Count}");
        Console.WriteLine($"inventory hooks={GameInventoryHooks.Read()}, experience={GameExperience.Read()}");
        Console.WriteLine(GameMoney.WithInventory(false,(handle,game,inventory,module)=>
        {
            var flags=System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic;
            foreach(long obj in new[]{game,inventory})
            {
                long cls=GameMoney.ReadInt64(handle,obj+0x18), array=GameMoney.ReadInt64(handle,cls+0x48);
                int count=GameMoney.ReadInt32(handle,cls+0x50);
                var functions=Enumerable.Range(0,count).Select(i=>GameMoney.ReadInt64(handle,array+i*8)).ToArray();
                var names=GameInventory.ReadNames(handle,module,functions.Select(fn=>GameMoney.ReadInt32(handle,fn+0x10)));
                foreach(long fn in functions)
                {
                    string name=names.GetValueOrDefault(GameMoney.ReadInt32(handle,fn+0x10),"");
                    if(name.Contains("Tick") || name.Contains("Ammo") || GameMoney.ReadInt32(handle,fn+0x78)==196 || GameMoney.ReadInt32(handle,fn+0x90)==67 || GameMoney.ReadInt32(handle,fn+0x10)==22391) Console.WriteLine($"metadata {name}: id={GameMoney.ReadInt32(handle,fn+0x10)}, flags={GameMoney.ReadInt32(handle,fn+0x18):X}, +78={GameMoney.ReadInt32(handle,fn+0x78)}, +90={GameMoney.ReadInt32(handle,fn+0x90)}");
                }
            }            foreach(string name in new[]{"FindTick","FindAmmoFunction"})
            {
                long obj=name=="FindTick" ? game : inventory;
                var fn=typeof(GameItemScheduler).GetMethod(name,flags)!;
                Console.WriteLine($"{name}: 0x{(long)fn.Invoke(null,[handle,obj,module])!:X}");
            }
            return "Version structure and function metadata verified; read-only";
        }));
    }
    else if(args.Length==1 && args[0]=="equipment-level-selftest")
    {
        var cases=new[] {("Steel Vixen",11),("Silver Vixen",11),("White Tiger Armor",11),("White Tiger Boots",11),
            ("White Tiger Pants",11),("White Tiger Gloves",11),("DLC13 Nilfgaardian Crossbow",7)};
        foreach(var (name,level) in cases)
        {
            var profile=GameEquipmentLevels.Reference(name)!;
            if(GameEquipmentLevels.Range(profile)!=(level,level)) throw new Exception($"Reference level mismatch: {name}");
        }
        var armor=GameEquipmentLevels.Reference("White Tiger Armor")!;
        if(GameEquipmentLevels.Range(armor,new Dictionary<string,decimal>{{"armor",90}})!=(12,12)) throw new Exception("Armor preview mismatch");
        var steel=GameEquipmentLevels.Reference("Steel Vixen")!;
        if(GameEquipmentLevels.Range(steel,new Dictionary<string,decimal>{{"damage",129}})!=(12,12)) throw new Exception("Sword preview mismatch");
        if(GameEquipmentLevels.Range(steel,new Dictionary<string,decimal>{{"criticalChance",80}})!=(11,11)) throw new Exception("Unrelated affix changed level");
        if(GameEquipmentLevels.Range(GameEquipmentLevels.Reference("Body underwear 01")) is not null) throw new Exception("Unknown base became a fixed level");
        Console.WriteLine("Reference levels, damage/armor previews, unaffected affix and unknown values passed; no game access");
    }
    else if(args.Length==1 && args[0]=="equipment-level-read")
    {
        GameEquipmentLevels.NewGamePlus=GameDebugFacts.ReadNamed("NewGamePlus");
        foreach(var item in GameInventory.Read().Where(item=>item.IsEquipment && !item.Name.StartsWith("Body "))
            .GroupBy(item=>item.Category).Select(group=>group.First()).Take(7))
        {
            var level=GameEquipmentLevels.Read(item);
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new {item.Name,item.UniqueId,level.Level,level.Values,
                Reference=GameEquipmentLevels.Reference(item.Name)}));
        }
    }
    else if(args.Length==1 && args[0]=="gwent-win")
    {
        GameItemScheduler.WinGwent();
        Console.WriteLine("Gwent player victory state confirmed.");
    }
    else if(args.Length==1 && args[0]=="equipment-instance-metadata-read")
    {
        var metadata=GameMoney.WithInventory(false,(handle,_,inventory,module)=>
        {
            var resolver=typeof(GameItemScheduler).GetMethod("ResolveName",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!;
            var functions=new List<object>();
            foreach(string name in new[]{"GetItemAttributeValue","GetItemModifierFloat","SetItemModifierFloat","GetItemModifierInt","SetItemModifierInt","GetItemTemplateOverride","SetItemTemplateOverride","AddItemBaseAbility"})
            {
                int id=(int)resolver.Invoke(null,[handle,module,name])!;
                long cls=GameMoney.ReadInt64(handle,inventory+0x18);
                for(int depth=0;cls!=0 && depth<16;depth++,cls=GameMoney.ReadInt64(handle,cls+0x10))
                {
                    int count=GameMoney.ReadInt32(handle,cls+0x50);
                    if(count is <0 or >4096) throw new InvalidOperationException("Class function bounds");
                    long table=GameMoney.ReadInt64(handle,cls+0x48);
                    for(int index=0;index<count;index++)
                    {
                        long function=GameMoney.ReadInt64(handle,table+index*8);
                        if(GameMoney.ReadInt32(handle,function+0x10)!=id) continue;
                        byte[] bytes=GameMoney.ReadBytes(handle,function,0xC0);
                        var pointers=new Dictionary<string,string>();
                        for(int offset=0;offset<=bytes.Length-8;offset+=8)
                        {
                            long value=BitConverter.ToInt64(bytes,offset);
                            if(value>=module && value<module+0x6000000) pointers[$"0x{offset:X}"]=$"0x{value-module:X}";
                        }
                        functions.Add(new {Name=name,Function=$"0x{function:X}",ModulePointers=pointers,Hex=Convert.ToHexString(bytes)});
                    }
                }
            }
            var evidence=File.ReadLines("Research/affix-persistence-state.jsonl")
                .Select(line=>System.Text.Json.JsonDocument.Parse(line)).ToArray();
            var ids=evidence.Where(document=>document.RootElement.GetProperty("Stage").GetString()=="Prepared")
                .SelectMany(document=>document.RootElement.GetProperty("Crafted").EnumerateArray().Select(value=>value.GetInt32())).Distinct().ToArray();
            var names=GameInventory.ReadNames(handle,module,ids);
            foreach(var document in evidence) document.Dispose();
            long vtable=GameMoney.ReadInt64(handle,inventory);
            if(vtable<module || vtable>=module+0x6000000) throw new InvalidOperationException("Inventory virtual table bounds");
            var virtuals=new Dictionary<int,string>();
            for(int index=0;index<128;index++)
            {
                long value=GameMoney.ReadInt64(handle,vtable+index*8);
                if(value>=module && value<module+0x6000000) virtuals[index]=$"0x{value-module:X}";
            }
            return new {Inventory=$"0x{inventory:X}",Functions=functions,KnownAffixNames=names,InventoryVirtuals=virtuals};
        });
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(metadata,new System.Text.Json.JsonSerializerOptions {WriteIndented=true}));
    }
    else if(args.Length==1 && args[0]=="equipment-level-metadata-read")
    {
        var metadata=GameMoney.WithInventory(false,(handle,_,inventory,module)=>
        {
            var finder=typeof(GameItemScheduler).GetMethod("FindAuxiliaryFunction",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!;
            long function=(long)finder.Invoke(null,[handle,module,inventory,"GetItemLevel","CInventoryComponent",1,false])!;
            int length=GameMoney.ReadInt32(handle,function+0x90);
            if(length is <1 or >65535) throw new InvalidOperationException("Script bytecode size mismatch");
            long bytecode=GameMoney.ReadInt64(handle,function+0x88);
            byte[] code=GameMoney.ReadBytes(handle,bytecode,length);
            var references=new Dictionary<string,int>();
            var nameResolver=typeof(GameItemScheduler).GetMethod("ResolveName",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!;
            foreach(string name in new[]{"GetItemAttributeValue","GetItemModifierInt","GetItemModifierFloat","GetItemName","GetItemCategory","ItemHasTag"})
            {
                int nameId=(int)nameResolver.Invoke(null,[handle,module,name])!,occurrences=0;
                long cls=GameMoney.ReadInt64(handle,inventory+0x18);
                for(int depth=0;cls!=0 && depth<16;depth++,cls=GameMoney.ReadInt64(handle,cls+0x10))
                {
                    int count=GameMoney.ReadInt32(handle,cls+0x50);
                    if(count is <0 or >4096) throw new InvalidOperationException("Class function table bounds");
                    long table=GameMoney.ReadInt64(handle,cls+0x48);
                    for(int index=0;index<count;index++)
                    {
                        long candidate=GameMoney.ReadInt64(handle,table+index*8);
                        if(GameMoney.ReadInt32(handle,candidate+0x10)!=nameId) continue;
                        byte[] pointer=BitConverter.GetBytes(candidate);
                        for(int offset=0;offset<=code.Length-8;offset++) if(code.AsSpan(offset,8).SequenceEqual(pointer)) occurrences++;
                    }
                }
                references[name]=occurrences;
            }
            return new {Function=$"0x{function:X}",Inventory=$"0x{inventory:X}",Length=length,EmbeddedFunctionReferences=references,Bytecode=Convert.ToHexString(code)};
        });
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(metadata,new System.Text.Json.JsonSerializerOptions {WriteIndented=true}));
    }
    else if(args.Length==2 && args[0]=="affix-persistence-prepare")
    {
        string evidence=Path.GetFullPath(args[1]);
        if(File.Exists(evidence)) throw new InvalidOperationException("Persistence evidence already exists; inspect it before generating more specimens");
        GameItemScheduler.RunSerialized(()=>
        {
            foreach(var specimen in new[]{(Name:"Viper School steel sword",Category:"steelsword"),(Name:"Bear Armor",Category:"armor")})
            {
                int id=GameEquipment.Create(specimen.Name,[],"",0);
                void Record(string stage,Dictionary<string,decimal> values) => File.AppendAllText(evidence,
                    System.Text.Json.JsonSerializer.Serialize(new {Stage=stage,Id=id,specimen.Name,specimen.Category,Values=values,Crafted=GameItemScheduler.ReadCraftedAbilities(id)})+Environment.NewLine);
                Record("Created",new());
                var original=GameEquipmentNumbers.Read(id,specimen.Category);
                var targets=new Dictionary<string,decimal>();
                foreach(var field in GameEquipmentNumbers.Fields.Where(field=>field.SeedUnit && field.Categories.Split(',').Contains(specimen.Category)))
                {
                    decimal step=GameItemScheduler.ReadSeedAbilityUnit(id,field.Abilities[0],field.Attribute)/field.Unit;
                    targets[field.Id]=Math.Round(original[field.Id]+2*step,3);
                }
                var actual=GameEquipmentNumbers.Apply(id,specimen.Category,targets);
                Record("Prepared",actual);
                Console.WriteLine($"Prepared persistence specimen {id}: {specimen.Name}; {System.Text.Json.JsonSerializer.Serialize(actual)}");
            }
            return 0;
        });
    }
    else if(args.Length==2 && args[0] is "affix-persistence-verify" or "affix-persistence-cleanup")
    {
        var specimens=File.ReadAllLines(args[1]).Select(line=>System.Text.Json.JsonDocument.Parse(line)).ToArray();
        try
        {
            var prepared=specimens.Select(document=>document.RootElement).Where(row=>row.GetProperty("Stage").GetString()=="Prepared").ToArray();
            if(prepared.Length!=2) throw new InvalidOperationException("Expected two complete persistence specimens");
            GameItemScheduler.RunSerialized(()=>
            {
                var inventory=GameInventory.Read();
                foreach(var specimen in prepared)
                {
                    int id=specimen.GetProperty("Id").GetInt32();
                    var item=inventory.SingleOrDefault(item=>item.UniqueId==id);
                    if(item is null || item.Name!=specimen.GetProperty("Name").GetString() || item.Category!=specimen.GetProperty("Category").GetString())
                        throw new InvalidOperationException($"Persistence specimen {id} missing or identity changed");
                    int[] expected=specimen.GetProperty("Crafted").EnumerateArray().Select(value=>value.GetInt32()).Order().ToArray();
                    if(!expected.SequenceEqual(GameItemScheduler.ReadCraftedAbilities(id).Order()))
                        throw new InvalidOperationException($"Persistence specimen {id}: crafted abilities differ");
                    var actual=GameEquipmentNumbers.Read(id,item.Category);
                    foreach(var value in specimen.GetProperty("Values").EnumerateObject())
                        if(Math.Abs(actual[value.Name]-value.Value.GetDecimal())>.001m)
                            throw new InvalidOperationException($"Persistence specimen {id}: {value.Name} differs");
                    Console.WriteLine($"Persistence specimen {id}: native values and crafted abilities match; read only");
                }
                if(args[0]=="affix-persistence-cleanup")
                    foreach(var specimen in prepared)
                    {
                        int id=specimen.GetProperty("Id").GetInt32();
                        var item=inventory.Single(item=>item.UniqueId==id);
                        if(item.Quantity!=1) throw new InvalidOperationException($"Persistence specimen {id}: cleanup quantity differs");
                        if(GameItemScheduler.IsItemHeld(id) || GameItemScheduler.IsItemMounted(id))
                        {
                            Console.WriteLine($"Persistence specimen {id}: equipped; kept for uninterrupted play");
                            continue;
                        }
                        Console.WriteLine($"Persistence specimen {id}: removed {GameInventory.Delete(item,1)} own test item");
                    }
                return 0;
            });
        }
        finally {foreach(var document in specimens) document.Dispose();}
    }
    else if(args.Length==1 && args[0]=="affix-runtime-selftest")
    {
        GameItemScheduler.RunSerialized(()=>
        {
            foreach(var specimen in new[]{(Name:"Viper School steel sword",Category:"steelsword"),(Name:"Bear Armor",Category:"armor")})
            {
                int id=GameEquipment.Create(specimen.Name,[],"",0);
                Console.WriteLine($"Temporary specimen {specimen.Name}: {id}");
                try
                {
                    foreach(var field in GameEquipmentNumbers.Fields.Skip(6).Where(field=>field.Categories.Split(',').Contains(specimen.Category)))
                    {
                        var readValue=typeof(GameEquipmentNumbers).GetMethod("Value",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!;
                        decimal before=(decimal)readValue.Invoke(null,[field,GameItemScheduler.ReadEquipmentAttributeParts(id,field.Attribute)])!;
                        decimal step=field.SeedUnit ? GameItemScheduler.ReadSeedAbilityUnit(id,field.Abilities[0],field.Attribute)/field.Unit : field.Units![0];
                        decimal target=Math.Round(before+2*step,3);
                        var applied=GameEquipmentNumbers.Apply(id,specimen.Category,new Dictionary<string,decimal>{{field.Id,target}});
                        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new {Specimen=id,field.Id,Before=before,Step=step,Target=target,Actual=applied[field.Id]}));
                        if(field.SeedUnit)
                        {
                            decimal lower=Math.Round(before+step,3);
                            GameEquipmentNumbers.Apply(id,specimen.Category,new Dictionary<string,decimal>{{field.Id,lower}});
                            Console.WriteLine($"Confirmed decrease {field.Id}: {lower}");
                        }
                    }
                }
                finally
                {
                    var item=GameInventory.Read().SingleOrDefault(item=>item.UniqueId==id);
                    if(item is not null)
                    {
                        if(item.Name!=specimen.Name || item.Quantity!=1) throw new Exception("Specimen identity changed; cleanup stopped");
                        Console.WriteLine($"Specimen cleanup {id}: {GameInventory.Delete(item,1)}");
                    }
                }
            }
            return 0;
        });
    }
    else if(args.Length==2 && args[0]=="seed-evaluator-selftest")
    {
        using var fixtures=System.Text.Json.JsonDocument.Parse(File.ReadAllText(args[1]));
        foreach(var fixture in fixtures.RootElement.EnumerateArray())
        {
            float actual=GameItemScheduler.EvaluateSeedUnit(fixture.GetProperty("First").GetSingle(),fixture.GetProperty("Second").GetSingle(),fixture.GetProperty("Seed").GetUInt16(),fixture.GetProperty("Scale").GetSingle());
            float expected=fixture.GetProperty("Result").GetSingle();
            if(BitConverter.SingleToInt32Bits(actual)!=BitConverter.SingleToInt32Bits(expected))
                throw new Exception($"Seed evaluation mismatch: {fixture.GetProperty("Seed")}");
        }
        Console.WriteLine($"Native evaluator bitwise comparison passed: {fixtures.RootElement.GetArrayLength()} cases; no game access");
    }
    else if(args.Length==1 && args[0]=="affix-definitions-read")
    {
        var rows=GameMoney.WithInventory(false,(handle,game,_,module)=>
        {
            long definitions=GameMoney.ReadInt64(handle,game+0xFD68);
            int bucketCount=GameMoney.ReadInt32(handle,definitions+0x60);
            if(bucketCount is <1 or >100000) throw new InvalidOperationException("Definition bucket bounds");
            long buckets=GameMoney.ReadInt64(handle,definitions+0x80);
            var rows=new List<object>();
            foreach(var effect in GameEquipment.Presets.Affixes)
            {
                int id=GameItemScheduler.ResolveEquipmentAbility(effect.Id);
                long node=GameMoney.ReadInt64(handle,buckets+(uint)id%(uint)bucketCount*8);
                for(int index=0;node!=0 && index<10000;index++)
                {
                    if(GameMoney.ReadInt32(handle,node)==id && GameMoney.ReadInt32(handle,node+0x60)==id) break;
                    node=GameMoney.ReadInt64(handle,node+0x68);
                }
                if(node==0) throw new InvalidOperationException("Missing definition");
                long attributes=GameMoney.ReadInt64(handle,node+8);
                int count=GameMoney.ReadInt32(handle,node+0x10);
                if(count is <0 or >100) throw new InvalidOperationException("Attribute bounds");
                var values=new List<object>();
                for(int index=0;index<count;index++)
                {
                    byte[] data=GameMoney.ReadBytes(handle,attributes+index*24,24);
                    int nameId=BitConverter.ToInt32(data,0);
                    string name=GameInventory.ReadNames(handle,module,[nameId])[nameId];
                    values.Add(new {Name=name,Component=BitConverter.ToInt32(data,4),Random=data[8],Min=BitConverter.ToSingle(data,12),Max=BitConverter.ToSingle(data,16),Precision=unchecked((sbyte)data[20])});
                }
                rows.Add(new {effect.Id,Attributes=values});
            }
            return rows;
        });
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(rows,new System.Text.Json.JsonSerializerOptions {WriteIndented=true}));
    }
    else if(args.Length==1 && args[0]=="affix-ability-read")
    {
        foreach(var field in GameEquipmentNumbers.Fields.Skip(6))
            foreach(var ability in field.Abilities)
                Console.WriteLine($"{field.Id} {ability}: {GameItemScheduler.ResolveEquipmentAbility(ability)}");
    }
    else if(args.Length==2 && args[0]=="equipment-floor-read" && int.TryParse(args[1],out int floorId))
    {
        var item=GameInventory.Read().Single(item=>item.UniqueId==floorId);
        var values=GameEquipmentNumbers.Read(item.UniqueId,item.Category);
        var minimums=GameEquipmentNumbers.MinimumTargets(item.UniqueId,item.Category,values,GameItemScheduler.ReadCraftedAbilities(item.UniqueId));
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new {item.Name,item.UniqueId,Current=values.GetValueOrDefault("damage"),Minimum=minimums}));
    }
    else if(args.Length==1 && args[0]=="adrenaline-read")
    {
        var result=GameMoney.WithInventory(false,(handle,game,_,module)=>
        {
            long player=GameMoney.ReadInt64(handle,GameMoney.ReadInt64(handle,game+0xFD60)+8);
            long ability=GameMoney.ReadInt64(handle,GameMoney.ReadInt64(handle,GameMoney.ReadInt64(handle,player+0x20)+0x158)+8);
            var flags=System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic;
            long page=(long)typeof(GameItemScheduler).GetMethod("FindPage",flags)!.Invoke(null,[handle,module])!;
            float current=BitConverter.ToSingle((byte[])typeof(GameItemScheduler).GetMethod("ExecuteEquipmentNative",flags)!.Invoke(null,[0x1D78540L,(byte[])[4,..BitConverter.GetBytes(4),9,1,0x20],false,null,ability,null,false,false,false])!);
            return new {Current=current,Enabled=GameItemScheduler.ReadAuxiliary("unlimitedAdrenaline"),DrainIntercepts=page==0?0:GameMoney.ReadInt64(handle,page+0xA18)};
        });
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(result));
    }
    else if(args.Length==3 && args[0]=="auxiliary-set" && bool.TryParse(args[2],out bool auxiliaryEnabled))
    {
        Console.WriteLine($"{args[1]}: {GameItemScheduler.SetAuxiliary(args[1],auxiliaryEnabled)}");
    }
    else if(args.Length==2 && args[0]=="level-set" && bool.TryParse(args[1],out bool levelEnabled))
    {
        Console.WriteLine($"equipment level: {GameItemScheduler.SetEquipmentLevelBypass(levelEnabled)}");
    }
    else if(args.Length==3 && args[0]=="facts-set" && bool.TryParse(args[2],out bool factEnabled))
    {
        Console.WriteLine($"{args[1]}: {GameDebugFacts.Set(args[1],factEnabled)}");
    }
    else if(args.Length==1 && args[0]=="facts-read")
    {
        foreach(string feature in GameDebugFacts.Names.Keys) Console.WriteLine($"{feature}: {GameDebugFacts.Read(feature)}");
    }
    else if(args.Length==1 && args[0]=="facts-selftest")
    {
        foreach(var (feature,name) in GameDebugFacts.Names)
        {
            foreach(bool add in new[]{false,true})
            {
                byte[] encoded=GameDebugFacts.Arguments(feature,add);
                int length=BitConverter.ToInt32(encoded,1);
                if(encoded[0]!=7 || length!=name.Length || System.Text.Encoding.ASCII.GetString(encoded,5,length)!=name)
                    throw new Exception("Fact string literal encoding failed");
                int offset=5+length;
                if(add)
                {
                    if(encoded[offset++]!=4 || BitConverter.ToInt32(encoded,offset)!=1) throw new Exception("Fact value encoding failed");
                    offset+=4;
                    if(encoded[offset++]!=4 || BitConverter.ToInt32(encoded,offset)!=-1) throw new Exception("Fact lifetime encoding failed");
                    offset+=4;
                    if(encoded[offset++]!=9 || encoded[offset++]!=0 || encoded[offset++]!=0) throw new Exception("Optional fact arguments failed");
                }
                if(encoded[offset++]!=0x20 || offset!=encoded.Length) throw new Exception("Fact parameter boundary failed");
            }
        }
        bool rejected=false;
        try {GameDebugFacts.Arguments("unknown",true);} catch(KeyNotFoundException) {rejected=true;}
        if(!rejected) throw new Exception("Unknown fact feature was accepted");
        Console.WriteLine($"{GameDebugFacts.Names.Count} curated facts: string/value/lifetime/optional argument encoding passed; no game access");
    }
    else if (args.Length == 1 && args[0] == "numeric-selftest")
    {
        if(GameEquipment.Presets.Affixes.Count!=30 || GameEquipment.Presets.Affixes.Count(effect=>GameEquipmentNumbers.ForAffix(effect.Id) is not null)!=29 || GameEquipmentNumbers.ForAffix("MA_Indestructible") is not null)
            throw new Exception("Numeric affix coverage mismatch");
        foreach(var effect in GameEquipment.Presets.Affixes.Where(effect=>GameEquipmentNumbers.ForAffix(effect.Id) is not null))
            if(GameEquipmentNumbers.ForAffix(effect.Id)!.Categories!=effect.ApplicableCategories)
                throw new Exception($"Affix applicability mismatch: {effect.Id}");
        foreach(var incompatible in new[]{("Viper School steel sword","MA_SlashingResistance"),("Bear Armor","MA_PoisonChance")})
        {
            bool rejected=false;
            try {GameEquipment.Create(incompatible.Item1,[incompatible.Item2],"",0);}
            catch(InvalidOperationException ex) when(ex.Message.Contains("仅适用于")) {rejected=true;}
            if(!rejected) throw new Exception("Incompatible creation was not rejected before game access");
        }
        var seedPlan=GameEquipmentNumbers.PlanSeedAdjustment(12.345m,17.283m,2.469m,2);
        if(seedPlan.Add[0]!=2 || seedPlan.Remove[0]!=0) throw new Exception("Seed-unit increase mismatch");
        var lowerPlan=GameEquipmentNumbers.PlanSeedAdjustment(17.283m,12.345m,2.469m,4);
        if(lowerPlan.Remove[0]!=2) throw new Exception("Seed-unit decrease mismatch");
        bool seedRejected=false;
        try {GameEquipmentNumbers.PlanSeedAdjustment(12.345m,15m,2.469m,2);} catch(InvalidOperationException) {seedRejected=true;}
        if(!seedRejected) throw new Exception("Unreachable seed-unit target accepted");
        var readValue=typeof(GameEquipmentNumbers).GetMethod("Value",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!;
        foreach(var field in GameEquipmentNumbers.Fields.Skip(6))
        {
            var parts=new GameItemScheduler.AttributeValue(.11f,.22f,33f);
            decimal actual=(decimal)readValue.Invoke(null,[field,parts])!;
            decimal expected=(field.Component=="add"?.11m:field.Component=="mult"?.22m:33m)/field.Unit;
            if(Math.Abs(actual-expected)>.001m || GameEquipmentNumbers.ForAffix(field.Affix)!=field)
                throw new Exception($"Affix component or mapping mismatch: {field.Id}");
        }
        foreach(var units in new[]{new[]{2,3,5},new[]{7,8},new[]{10},new[]{1}})
            for(int amount=0;amount<=300;amount++)
            {
                bool reachable=Enumerable.Range(0,amount+1).Aggregate(new HashSet<int>{0},(set,value)=>
                { if(units.Any(unit=>value>=unit && set.Contains(value-unit))) set.Add(value); return set; }).Contains(amount);
                try
                {
                    var counts=GameEquipmentNumbers.Compose(amount,units);
                    if(!reachable || counts.Zip(units).Sum(pair=>pair.First*pair.Second)!=amount) throw new Exception("Composition mismatch");
                }
                catch(InvalidOperationException) { if(reachable) throw; }
            }
        var adjustment=GameEquipmentNumbers.PlanAdjustment(30,29,[2,3,5],[0,0,3]);
        if(adjustment.Add.Zip(new[]{2,3,5}).Sum(pair=>pair.First*pair.Second)-adjustment.Remove.Zip(new[]{2,3,5}).Sum(pair=>pair.First*pair.Second)!=-1)
            throw new Exception("Adjustment must achieve the requested decrease exactly");
        foreach(decimal impossible in new[]{14m,16m,29.5m})
        {
            bool rejected=false;
            try {GameEquipmentNumbers.PlanAdjustment(30,impossible,[2,3,5],[0,0,3]);}
            catch(InvalidOperationException) {rejected=true;}
            if(!rejected) throw new Exception("Unrepresentable target was accepted");
        }
        var untouched=GameEquipmentNumbers.PlanAdjustment(29.999999m,30,[2,3,5],[0,0,3]);
        if(untouched.Add.Sum()!=0 || untouched.Remove.Sum()!=0) throw new Exception("Float noise caused a write plan");
        bool belowNativeRejected=false;
        try {GameEquipmentNumbers.PlanAdjustment(142.5677m,133.5677m,[7,8],[0,0]);}
        catch(InvalidOperationException ex) {belowNativeRejected=ex.Message.Contains("固有下限 142.568");}
        if(!belowNativeRejected) throw new Exception("Below-native weapon target lacked a clear floor message");
        var removeUpgrade=GameEquipmentNumbers.PlanAdjustment(149.5677m,142.5677m,[7,8],[1,0]);
        if(removeUpgrade.Remove[0]!=1 || removeUpgrade.Add.Sum()!=0) throw new Exception("Removing an added upgrade must remain supported");
        Console.WriteLine("Native numeric composition reachability and exact totals passed");
    }
    else if (args.Length == 1 && args[0] == "catalog-selftest")
    {
        var catalog = GameItem.LoadCatalog();
        var sword = catalog.Single(item => item.Name == "Viper Steel sword schematic");
        if (!sword.Matches("毒蛇学派", false) || !sword.Matches("蛇派", false) || sword.Matches("蛇剑", false)
            || !sword.Matches("蛇剑", true) || sword.Matches("银剑", true))
            throw new InvalidOperationException("Catalog search behavior failed");
        if (catalog.Any(item => item.CategoryName == item.Category))
            throw new InvalidOperationException("Catalog category translation incomplete");
        Console.WriteLine($"Catalog search and {catalog.Select(item => item.Category).Distinct().Count()} Chinese categories passed");
    }
    else if (args.Length == 1 && args[0] == "selftest")
    {
        var method = typeof(GameInventoryHooks).GetMethod("MakeJump", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            ?? throw new InvalidOperationException("MakeJump method missing");
        byte[] actual = (byte[])method.Invoke(null, [0x1122334455667788L, 15])!;
        byte[] expected = Convert.FromHexString("FF2500000000887766554433221190");
        if (!actual.SequenceEqual(expected)) throw new InvalidOperationException($"Unexpected jump: {Convert.ToHexString(actual)}");
        Console.WriteLine("Jump encoding passed");
    }
    else if (args.Length == 0 || args[0] == "read")
    {
        var state = GameInventoryHooks.Read();
        Console.WriteLine($"Gold x{state.Gold:0.0}, keep items {state.KeepItems}");
        Console.WriteLine($"Crowns {GameMoney.Read()}");
        Console.WriteLine($"XP x{GameExperience.Read():0.0}; {GameExperience.Diagnostics()}");
    }
    else if (args.Length == 2 && args[0] == "gold" && decimal.TryParse(args[1], out decimal requestedGold))
    {
        bool keep = GameInventoryHooks.Read().KeepItems;
        var state = GameInventoryHooks.Set(requestedGold, keep);
        Console.WriteLine($"Gold x{state.Gold:0.0}, keep items {state.KeepItems}");
    }
    else if (args.Length == 2 && args[0] == "items" && bool.TryParse(args[1], out bool keep))
    {
        decimal currentGold = GameInventoryHooks.Read().Gold;
        var state = GameInventoryHooks.Set(currentGold, keep);
        Console.WriteLine($"Gold x{state.Gold:0.0}, keep items {state.KeepItems}");
    }
    else if (args.Length == 2 && args[0] == "xp" && decimal.TryParse(args[1], out decimal requestedXp))
    {
        Console.WriteLine($"XP x{GameExperience.Set(requestedXp):0.0}; {GameExperience.Diagnostics()}");
    }
    else if (args.Length == 1 && args[0] == "tick-install")
    {
        GameItemScheduler.InstallForProbe();
        Console.WriteLine($"Game tick count {GameItemScheduler.TickCount()}");
    }
    else if (args.Length == 1 && args[0] == "rebind")
    {
        Console.WriteLine($"Inventory rebound: {GameInventoryHooks.Rebind()}");
        var state = GameInventoryHooks.Read();
        Console.WriteLine($"Gold x{state.Gold:0.0}, keep items {state.KeepItems}");
    }
    else if (args.Length == 1 && args[0] == "tick")
    {
        Console.WriteLine($"Game tick count {GameItemScheduler.TickCount()}");
    }
    else if (args.Length == 1 && args[0] == "consumables")
    {
        Console.WriteLine($"Consumable removals intercepted {GameItemScheduler.ConsumableInterceptCount()}");
    }
    else if (args.Length == 3 && args[0] == "give" && int.TryParse(args[2], out int quantity))
    {
        var result = GameItemScheduler.Give(args[1], quantity);
        Console.WriteLine($"Item {args[1]}: quantity change {result.Added}, returned IDs {result.ReturnedIds}");
    }
    else if (args.Length == 1 && args[0] == "diagnostics")
    {
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(GameItemScheduler.Diagnostics()));
    }
    else if (args.Length == 1 && args[0] == "log-selftest")
    {
        var exception = new InvalidOperationException("Failure log self-test");
        if (!ErrorLog.Write("日志自检", exception, new { Name = "Silver ingot", Quantity = 1 })) throw new InvalidOperationException("Log write failed");
        using var record = System.Text.Json.JsonDocument.Parse(File.ReadLines(ErrorLog.Path).Last());
        if (!record.RootElement.GetProperty("Error").GetString()!.Contains(exception.Message) ||
            record.RootElement.GetProperty("Context").GetProperty("Quantity").GetInt32() != 1)
            throw new InvalidOperationException("Failure log content missing");
        Console.WriteLine($"Failure log JSON, exception and item context passed: {ErrorLog.Path}");
    }
    else if (args.Length == 1 && args[0] == "give-result-selftest")
    {
        if (GameItemScheduler.ConfirmGiveResult(1, 2, 3) != 1 || GameItemScheduler.ConfirmGiveResult(2, 2, 3) != 1)
            throw new InvalidOperationException("Added quantity must confirm success even without new IDs");
        try { GameItemScheduler.ConfirmGiveResult(1, 2, 2); throw new Exception("Expected unchanged quantity failure"); }
        catch (InvalidOperationException) { }
        Console.WriteLine("Give result: normal addition, merged stack and unchanged inventory passed");
    }
    else if(args.Length==2 && args[0]=="teleport-restore-job")
    {
        byte[] snapshot=File.ReadAllBytes(args[1]);
        using var metadata=System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.ChangeExtension(args[1],".json")));
        string hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(snapshot));
        if(snapshot.Length!=0x2000 || hash!=metadata.RootElement.GetProperty("SHA256").GetString())
            throw new InvalidOperationException("Snapshot integrity mismatch");
        using var gameProcess=System.Diagnostics.Process.GetProcessById(metadata.RootElement.GetProperty("PID").GetInt32());
        GameItemScheduler.RunSerialized(()=>GameMoney.WithInventory(true,(handle,_,_,module)=>
        {
            if(gameProcess.HasExited || module!=Convert.ToInt64(metadata.RootElement.GetProperty("Module").GetString(),16))
                throw new InvalidOperationException("Snapshot process changed");
            var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static;
            long page=(long)typeof(GameItemScheduler).GetMethod("FindPage",flags)!.Invoke(null,[handle,module])!;
            if(page!=Convert.ToInt64(metadata.RootElement.GetProperty("Page").GetString(),16))
                throw new InvalidOperationException("Snapshot page changed");
            byte[] expected=(byte[])typeof(GameTeleport).GetMethod("BuildCode",flags,null,[typeof(long)],null)!.Invoke(null,[module])!;
            byte[] original=snapshot.AsSpan(0x1A00,0x600).ToArray();
            GameProcessPause.Run(handle,()=>
            {
                if(GameMoney.ReadBytes(handle,page+0x90,1)[0]!=0 || GameMoney.ReadBytes(handle,page+0x10,1)[0]!=0)
                    throw new InvalidOperationException("Game request still active");
                byte[] current=GameMoney.ReadBytes(handle,page+0x1A00,0x600);
                if(current.SequenceEqual(original)) return;
                if(!current.AsSpan(0,expected.Length).SequenceEqual(expected) || current.AsSpan(expected.Length).IndexOfAnyExcept((byte)0)>=0)
                    throw new InvalidOperationException("Unexpected installed teleport job");
                typeof(GameItemScheduler).GetMethod("Patch",flags)!.Invoke(null,[handle,page+0x1A00,original]);
                if(!GameMoney.ReadBytes(handle,page+0x1A00,0x600).SequenceEqual(original))
                    throw new InvalidOperationException("Teleport restoration readback mismatch");
            },(page+0x1A00,0x600));
            return 0;
        },0x800));
        Console.WriteLine("Previous teleport slot restored and verified; other runtime state preserved.");
    }
    else if(args.Length==2 && args[0]=="teleport-confirm-read")
    {
        using var record=System.Text.Json.JsonDocument.Parse(File.ReadAllText(args[1]));
        var input=record.RootElement;
        using var process=System.Diagnostics.Process.GetProcessById(input.GetProperty("PID").GetInt32());
        if(process.ProcessName!="witcher3" || process.MainModule!.BaseAddress.ToInt64()!=input.GetProperty("Module").GetInt64())
            throw new InvalidOperationException("Confirmation process mismatch");
        var target=new GameTeleport.Target(input.GetProperty("Game").GetInt64(),input.GetProperty("Player").GetInt64(),input.GetProperty("Module").GetInt64(),0,input.GetProperty("Area").GetInt32(),0,0,default,0,0,"");
        var position=GameTeleport.ReadCurrentPosition(target);
        foreach(var changed in new[]{target with {Player=target.Player+8},target with {Area=target.Area+1}})
        {
            bool rejected=false;
            try {GameTeleport.ReadCurrentPosition(changed);} catch(InvalidOperationException) {rejected=true;}
            if(!rejected) throw new InvalidOperationException("Stale confirmation context accepted");
        }
        Console.WriteLine($"Current position {position}; confirmation independent of map pins; changed player/area rejected; read-only");
    }
    else if(args.Length==1 && args[0]=="teleport-code-check")
    {
        Console.WriteLine(GameMoney.WithInventory(false,(handle,_,_,module)=>
        {
            var flags=System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic;
            long page=(long)typeof(GameItemScheduler).GetMethod("FindPage",flags)!.Invoke(null,[handle,module])!;
            if(page==0) return "No scheduler installed";
            byte[] slot=GameMoney.ReadBytes(handle,page+0x1A00,0x600);
            return $"Known teleport slot: {GameTeleport.IsKnownCode(slot,module)}; SHA256: {Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(slot))}; read-only";
        }));
    }
    else if (args.Length == 1 && args[0] is "teleport-target" or "teleport-preview" or "teleport")
    {
        if (args[0] == "teleport-target") Console.WriteLine(GameTeleport.ReadTarget());
        else Console.WriteLine(GameTeleport.Execute(args[0] == "teleport"));
    }
    else if (args.Length == 2 && args[0] == "give-cycle-test")
    {
        string name = args[1];
        var before = GameInventory.Read().Where(item => item.Name == name).ToArray();
        Console.WriteLine($"Before: {string.Join(';', before.Select(item => $"ID {item.UniqueId} quantity {item.Quantity}"))}");
        var timer = System.Diagnostics.Stopwatch.StartNew();
        try { Console.WriteLine($"Give result: {GameItemScheduler.Give(name, 1)} in {timer.ElapsedMilliseconds}ms"); }
        catch (Exception ex) { Console.WriteLine($"Give exception after {timer.ElapsedMilliseconds}ms: {ex}"); }
        var after = GameInventory.Read().Where(item => item.Name == name).ToArray();
        int change = after.Sum(item => item.Quantity) - before.Sum(item => item.Quantity);
        Console.WriteLine($"After: {string.Join(';', after.Select(item => $"ID {item.UniqueId} quantity {item.Quantity}"))}; delta {change}");
        if (change == 1)
        {
            var changed = after.Single(item => item.Quantity - (before.SingleOrDefault(old => old.UniqueId == item.UniqueId)?.Quantity ?? 0) == 1);
            Console.WriteLine($"Removed own added unit: {GameInventory.Delete(changed, 1)}");
        }
    }
    else if (args.Length > 1 && args[0] == "names")
    {
        var names = GameMoney.WithInventory(false, (handle, _, _, module) => GameInventory.ReadNames(handle, module, args.Skip(1).Select(int.Parse)));
        foreach (var pair in names) Console.WriteLine($"{pair.Key}: {pair.Value}");
    }
    else if (args.Length == 1 && args[0] == "inventory")
    {
        var items = GameInventory.Read();
        Console.WriteLine($"Inventory entries {items.Count}");
        foreach (var item in items.Where(item => item.IsEquipment))
            if (GameItemScheduler.IsItemHeld(item.UniqueId) || GameItemScheduler.IsItemMounted(item.UniqueId))
                Console.WriteLine($"Equipped {item.Name} ID {item.UniqueId}; enchantment {GameItemScheduler.ReadEnchantment(item.UniqueId)}; crafted {string.Join(',', GameItemScheduler.ReadCraftedAbilities(item.UniqueId))}");
    }
    else if (args.Length >= 4 && args[0] == "gear-create" && int.TryParse(args[2], out int slots))
    {
        int id = GameEquipment.Create(args[1], args.Skip(4), args[3] == "none" ? "" : args[3], slots);
        Console.WriteLine($"Created equipment {args[1]} ID {id}: slots {GameItemScheduler.ReadEquipmentSlots(id)}, crafted {string.Join(',', GameItemScheduler.ReadCraftedAbilities(id))}, enchantment {GameItemScheduler.ReadEnchantment(id)}");
    }
    else if (args.Length == 2 && args[0] == "gear-cycle-test")
    {
        var flags = GameInventoryHooks.Read();
        int id = GameEquipment.Create(args[1], [], "", 0);
        Console.WriteLine($"Own test equipment created: {id}");
        var item = GameInventory.Read().Single(item => item.UniqueId == id);
        var initial = GameEquipment.Inspect(item);
        bool armor = item.Category == "armor";
        var edited = GameEquipment.Edit(item, [armor ? "MA_Vitality" : "MA_CriticalChance"], armor ? "Glyphword 1" : "Runeword 1", 3);
        Console.WriteLine($"Edited: slots {edited.Slots}, enchant {edited.Enchantment}, abilities {string.Join(',', edited.Crafted)}");
        var cleared = GameEquipment.Edit(item, [], "", 3);
        Console.WriteLine($"Cleared: enchant {cleared.Enchantment}, abilities {string.Join(',', cleared.Crafted)}");
        Console.WriteLine($"Deleted own test equipment: {GameInventory.Delete(item, 1)}");
        var after = GameInventoryHooks.Read();
        if (flags != after) throw new InvalidOperationException("Global inventory settings changed during explicit deletion");
        Console.WriteLine($"Global settings unchanged: gold {after.Gold}, keep {after.KeepItems}; initial slots {initial.Slots}");
    }
    else if (args.Length == 2 && args[0] == "gear-ability-id")
    {
        Console.WriteLine($"Ability {args[1]}: {GameItemScheduler.ResolveEquipmentAbility(args[1])}");
    }
    else if (args.Length == 2 && args[0] == "gear-crafted" && int.TryParse(args[1], out int craftedId))
    {
        Console.WriteLine($"Item {craftedId}: crafted abilities {string.Join(',', GameItemScheduler.ReadCraftedAbilities(craftedId))}");
    }
    else if (args.Length == 2 && args[0] == "gear-slots" && int.TryParse(args[1], out int uniqueId))
    {
        Console.WriteLine($"Item {uniqueId}: slots {GameItemScheduler.ReadEquipmentSlots(uniqueId)}");
    }
    else if (args.Length == 4 && args[0] == "gear-numeric-set" && int.TryParse(args[1],out int numberId) && decimal.TryParse(args[3],out decimal target))
    {
        var result=GameItemScheduler.RunSerialized(()=>GameEquipmentNumbers.Apply(numberId,"steelsword",new Dictionary<string,decimal>{{args[2],target}}));
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(result));
    }
    else if (args.Length == 1 && args[0] == "gear-numeric-probe")
    {
        int id = GameEquipment.Create("Viper School steel sword", [], "", 0);
        Console.WriteLine($"Numeric test equipment ID {id}");
        var attributes = new[] { "SlashingDamage", "desc_poinsonchance_mult", "buff_apply_chance" };
        var before = attributes.ToDictionary(name => name, name => GameItemScheduler.ReadEquipmentAttributeParts(id,name));
        for (int count=0; count<2; count++) GameItemScheduler.AddEquipmentAbility(id,"Rune morana greater _Stats",true);
        GameItemScheduler.AddEquipmentAbility(id,"autogen_fixed_steel_dmg",true);
        var after = attributes.ToDictionary(name => name, name => GameItemScheduler.ReadEquipmentAttributeParts(id,name));
        var result = new { Id=id, BaseItem="Viper School steel sword", Before=before, After=after, Crafted=GameItemScheduler.ReadCraftedAbilities(id) };
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(result));
        if(Math.Abs(after[attributes[1]].Additive-before[attributes[1]].Additive-.10f)>.0001f ||
            Math.Abs(after[attributes[0]].Base-before[attributes[0]].Base-8)>.0001f)
            throw new InvalidOperationException("Numeric ability composition readback failed");
    }
    else if (args.Length == 3 && args[0] == "gear-attribute" && int.TryParse(args[1], out int attributeItemId))
    {
        Console.WriteLine($"Item {attributeItemId}: {args[2]} {GameItemScheduler.ReadEquipmentAttribute(attributeItemId, args[2])}");
    }
    else
    {
        Console.Error.WriteLine("Usage: read | gold <1.0..100.0> | xp <1.0..100.0> | items <true|false> | tick-install | tick | give <item name> <quantity>");
        Environment.ExitCode = 2;
    }
}
catch (Exception error)
{
    Console.Error.WriteLine(error);
    Environment.ExitCode = 1;
}
