# Marketing Tools - AB Testing

- [AB Testing Overview](#overview)
- [Development environment](#development-environment)
- [Building with Cake](#building-with-cake)
- [Signing Assemblies](#signing-assemblies)

## Overview
A/B testing lets you create variations for a number of page elements (blocks, images, content, buttons, form fields, and so on), then compare which variation performs best. It measures the number of conversions obtained from the original (control) versus the variation (challenger), and the one that generates the most conversions during the testing period is typically promoted to the design for that page. Episerver A/B testing has several predefined conversion goals you can use when setting up a test, and it is also possible for Episerver developers to create customized conversion goals.

For more information and an overview of how to use the AB Testing package in your solution visit the AB Testing user guide which can be found here: https://webhelp.optimizely.com/21-1/en/cms-edit/ab-testing.htm

## Development environment 
### Prerequisites
* Visual Studio 2019 (or higher)
* Cms 11.x 

#### NuGet Package Restore
This project makes use of the NuGet *package restore* functionality to fetch our dependencies, for this to work properly the following is required in the NuGet configuration:
* The following package sources are required:
    * https://www.nuget.org/api/v2/
    * http://nuget.episerver.com/feed/packages.svc/
* To make use of both package sources at the same time select *All* as the *Package Source*
* The *Package Restore* setting needs to be enabled

### Building with Cake
This project uses cake script for the build and will create nuget artifacts for convience. For more information related to Cake refer to the documentation which can be found here https://cakebuild.net/.

The scripts are located in the BuildScripts folder. 

1. build.ps1 - This contains bootstrapper script for powershell and shouldn't be changed.
2. build.cake - This is the cake script file that contains various tasks required to clean, restore, build, run tests and create nuget packages. This follows C# syntax and can be modified as per requirements. 
3. tools - This folder contains different tools required by build.cake script to run successfully.

To run the script, execute the following command in a powershell window:

```
.\build.ps1 [-configuration <"Debug"|"Release">] [-target "<taskname>" ]
```
```
Example: .\build.ps1 -configuration "Debug" -target "PackageNuGets"
```
If no task is specified as the target, the "Default" task is run. 
After a successful run, the nuget packages are created in the "Artifacts" folder, which is on the same level as the "BuildScripts" folder.

### Signing Assemblies
All the assemblies are delay signed with a strong name key file "EPiServerProduct_PublicKey.snk" located in the build folder.
The signing is completed on Team City by a key file containing the private key.

For the assemblies to work on development machines, they have to be registered for verification skipping by running the following commands in a command window.

```
cd C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.8 Tools
sn -Vr *,8fe83dea738b45b7
```

```
cd C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.8 Tools\x64
sn -Vr *,8fe83dea738b45b7
```

To unregister the assemblies from verification skipping:

```
sn -Vu *,8fe83dea738b45b7
```
OR

```
sn -Vx (unregisters all assemblies)
```

Note: The "EPiServerProduct_PublicKey.snk" file can be moved to "BuildScripts" folder and the build folder can be deleted. The rest of the files in that folder are not used anymore.
If this is done, update the path to the snk file in the project properties of each project in the solution.

```
Right-click project -> Properties -> Signing <Update the path here>
```

### Updating Nuget package versions.
The versioning of the assemblies and nuget packages is done in the build.cake file. Each package is independently versioned. Refer to the task "Version" in the script that uses the values of the following variables to create assembly versions, which are then used in the individual tasks that generate the nuget packages i.e. "PackageKpi", "PackageKpiCommerce", "PackageMessaging" and "PackageABTesting".

```
var kpiBaseVersion = "2.5.3";  // For EPiServer.Marketing.KPI package

var kpiCommerceBaseVersion = "2.4.2"; // EPiServer.Marketing.KPI.Commerce

var messagingBaseVersion = "1.3.0";  // EPiServer.Marketing.Messaging

var webBaseVersion = "2.6.0";  // EPiServer.Marketing.Testing

```

#### Adding migration scripts.
A/B testing uses Entity framework to install and update the schema. These directions explain how to migrate 
the schema to a newer version

1. Add ..\episerver.marketing.testing\Models\Testing project to the solution.
2. Make sure the connection string in the app.config points to a database for a site that has A/B testing package installed.
```
  <add name="EPiServerDB" connectionString="data source=(LocalDB)\MSSQLLocalDB;AttachDbFilename=C:\Users\hemc\source\repos\AllowWithDelivra\AllowWithDelivra\App_Data\EPiServerDB_3a7f9c8d.mdf;Integrated Security=True;Connect Timeout=30"
          providerName="System.Data.SqlClient" />
```
3. Build the project.
4. Open package manager console, select the Testing project in the default project dropdown. Run the following command.
```
update-database -ConnectionStringName EPiServerDB
```
If you run into the following error, 
"Unable to update database to match the current model because there are pending changes and automatic migration is disabled."

Run the following command, and repeat step# 4.
```
enable-migrations –EnableAutomaticMigration:$true -Force
```
5. Creates Entity Framework specific classes by running the following command. 
```
add-migration <MigrationName>
```
These are the classes that need to be modified to perform the update. There are examples
of previous migrations in the project.

6. Generate the sql script by running the following command.
```
Update-Database -Script -SourceMigration: $InitialDatabase
```
Copy the section that matches the MigrationName. This is at the bottom of the script file and it should look
something like this.
```
IF @CurrentMigration < '202002251904599_MigrationName'
BEGIN
    <SQL from the DbMigration.Up() method will be inserted automatically here>
    INSERT [dbo].[__MigrationHistory]([MigrationId], [ContextKey], [Model], [ProductVersion])
    VALUES (N'202002251904599_MigrationName', N'Testing.Migrations.Configuration',  0x1F8B0800000000000400ED5D4B6FE43612BE2FB0FF41D069773171DB3397ACD19DC0E347D0C8786C4C7B268BBD0C6889DD2622511D51726C04FBCB72C84FDABFB0A5D68B0F512225B9DDBD69E490B6487E6415AB8AC547D5FCF7F73FA6DF3F8581F3886346223A734F8E8E5D07532FF2095DCDDC34597EF3ADFBFD777FFDCBF4D20F9F9C2F65BD77593D6849D9CC7D4892F5E964C2BC071C227614122F8E58B44C8EBC289C203F9ABC3D3EFEE7E4E4648201C2052CC7997E4A694242BCF903FE3C8FA887D7498A82EBC8C7012BBE43C96283EA7C4421666BE4E1997B794B163886111F5DA3F8679CC0408FEE30DBFCFF0205AE73161004C35AE060E93A88D22841090CFAF433C38B248EE86AB1860F28B87B5E63A8B74401C30531A7757553BA8EDF66744DEA86259497B2240A2D014FDE158C9AC8CD7BB1DBAD1809ACBC049627CF19D51B76CE5CE0D6D9FB8C75AE23F7767A1EC459CD4E761FE5A89B693BAA00DF3845953795C8806465FFBD71CED32049633CA3384D6214BC716ED3FB80783FE2E7BBE8674C67340D027EDC307228133EC0A7DB385AE33879FE8497053573DF752662BB89DCB06AC6B5C9C9FC2125F0FB23F48DEE035C49C5A4B5F91D49025C22806C01C1AE738D9E3E60BA4A1E662EFC749D2BF284FDF24B01FB991250316894C429B6EEF602332F26EB5C2A0677DEDED7CDAF14C7DB27F126262B4251304F703870921620D1F5248151C8C43413CFA2C01E2E4E2E38C8ECF71DD8326BA44BEA8F82730B03221E596F14F716C71EA6095A55B8739ABC7B6B0DFA01B104949A2C09F6DF3FB708C0C9F1F1086276F9B4C65E82FD2F8491248ACF23581F2402DA01CEBC6CF568696E493FAC484BE2C34A883FE0471C54931481B1B29FA27F2FBC28C60341E66C41561466C4433575EFA328C088F6A12F0139F980E82AE584657B1A7E1E6310F87134A014D47E601FD12359E5CA23C2C2EA01EAB48C6258183D3CA73E701EA48BB9CE271C6C1AB007B2AE8D8AA6FE576E9DBD8AA3F05314E48B6F67FDAF77285EE16CAE238B468B28051B604EE617141390282D5945B9960CA5BC69D86AA5A6614E27B593D2E5BA681831A22FA3E9E1CFE2DC00910321340C9451F7DE50709AB12DCB506A8F956528F5B2AFCA154A3CA28A15880795327602863BC41944C1E5613ED29CFD4428B73D18E08B1407106CA08BF485E05FD950A236F2C51EB03F98AEFF03CB65BFF8375826BD8760314ED0CD5A503E610666A1CD1393EB7EE5ED976C6C5B2A6B7CB0B616B60E588E7805FB5DEA111474D326556D254D5F5743594B837E847D41418ABB89E2AAB512D45C4F438CA6F208BEA72C00E3FA9D32FA9F6481FC714D062234AC25FDD6819F30593D2403D72310EFCDB18608B6BD4D36E70F0EE346A1384327672F16C4CAF2BCFCDAD2ECC59BAC46032C9764E0C7355C12F8C16E996E0D80F7F52923F64898FD791BC3AFE266EE5BD759782803B457DF0D7A3E8505D75FE1C06F23D5A0FE2F4BAAD8CDAB517B30982FE0B0369B4B030F7780B5E43CC8712D25077CB092C65A15A443AF510E9A39E2AEAB59233BB66866DA9886A22E7257B6737615A055FD76631CB5CCBA64C2E5F060BD8445C4C771F00C10BCF72D4EE0350EEF715CAA18455E421E81A80D1367EEB132E142FD33B1F6497BED8B88D675D53B551139F61E00DAAFEABF53672E9F23FEE3196311D8E26C163AAF6AB84325711C97D477FA9C850B57FCDAFBA16B9839B286B982999FB9FF50B860D97B75F020F45E1E96899D9DB8B261BEA117B0474CB0934D64E6FC9C23E6215F5573E0B62F7E015B8E634CB3A74CE0F43090424213D5F0139ABD0D087A902461192E23D958AB5EE5920BBCC6A01234E931BF26C3294FD8D521553D4B6CEDE2E274C28974A7A4ABC7A52DB2D572762AC8526594AD24B7E55A765F24554BC27624533B3FFB20891DC714ED26CFE8CC42B6B6EA89A8ADA5353A8637518D9D905E3382B666640DE6D4642C9CFBFC8AA2DDB2A16C173193DDA52CD8CA8199AD5C9BDCC1EC935877D3B335A9EE9ECFFD116ACD5EAC5DB6BA3666B2300BE719B682DC75F7B64F42DC4ECBD604B87DFE764F78F31D73F67C145AD41B4B94A07BC4F0E659E953D349D96728CC77E5AC385295452DC35DE0A4DC7E6E3C2EE63AF5F980EAB32AE22A62E81F7036816A378C1DBDD4EF279B502B2DE840D1BE70D00C55F5B68C3A505F1968F09545CF085EBCEBD7400B264882E5A44EC6EE7A41C735B5797927AB4CCFC3878A1F6D62A7E867CFB306AEB34A4F645B2972D28CCB0DAF7D1AB9DAB1CD35DFE87284D46AD4CE25FDBEF6A5B8D275E3AB133CE3FD57CF1D1847AFD67A744A9CC99ECB60967A73B6ED6A48C75853EFBF9FFFAFB055B5999D5C35F0F85F92A9BA537D1D434D3C4F7BDF5361A4B83A7432B1C3DB1CC6C0F2E2A1F264AAB2E9240F3A2D3E4C279AE8D4E9355AAF095D71D1AAC517679187AA9E7FB3B00FDB0C738C89C71AA237ABD1563DC1028156582ACDAEC17D7C456296949E99EB9CFBA1524DF6DB346B7CD99BE89AA9F3572EF765FDEC7711E0D57111A35D496AAE5E01A161E63E67346351B8746D9D2C821805286EB8C93C8F8234A47A575EDFBA88FBE4018A4FE6184210278F241498E315819A3C52F1C902438ABA14C0A43273D4E2D28E072B3E5961945197124EF9D91CAB8ABBE491AA8FE638DAB84B1E575BC9BC1F3914938797CB2CB8D0187129B0A4B186790F4D21993C7E53B939BA12A0C9432B85E6B865A8260F577E33479162350553231659512C866C4A148B8516B8FC530601932F30C7139F33F08062898A389D48A65E39CE509618E580485CB18CD633ED7E6DE4054E77C0D06BC533067BA125B0B8DE12D640CD95971EA5254090076EA97610736331AF9CE491C55AB35B3013636DE35D16DB26FFC8DE2F12E2F064A8AAC066CD29C3F1C4E5A6FC6AB5D2D44114D22A531798E31561793C52F1C9863A2E2E4F24902B3898036373A03B321A7FD1538EAAFB2E78DD402F63358A87ABC292947F7A3D9D2A438A78A8F29BC5BE4A0A501236575299C5CE880F3B1276437C8185FDA86FB8041BA2BFF86A998783DE379E698EAFF6F20D525FADEFC4D95DA52F425D046F23FF648921C5EE288052B9AD59AAA37254CB5497F5456D1CB6A6CAC128BCA25110CEE7C73708FCBD6F5F63D08AB1BB86A0085C11053550F3A2B5631C04DE52E0954B1EB94AD57B75D9235DEA4C8B0B96EEBCA4CA8D4B5E258B6C8C1E899FDDB62C9E196CED8EB20A478B5F82F380E0EC08B0AC708D285982D6E40124EEDBE393B75236D3DDC92C3A61CC0F1A2EA80CD38BCEA98F9F66EE6FCE7FB61FBB9652F24B8AB3E3E12493CC7858DA4FFA8862EF01C57F0BD1D3DFC748E5A905B44ED7396C68CD293807334F48C74932F11F9A80D387DFC90809387BE37424E0EC436473FACD7242D5049C7652D2966D531AAD099C3EF7661FD2359937974184ECC1C4C49BFD301AF36EDE935E9435E5DC1CA6A60D81A8BD05B92910D510EC653237EEF73221647B53E1F4D466ED4F9DF9BFBEE6106F9C9B18DC8353E718F8307A0EC6363A4DB47F0F05509FC7F0207083056E647FA1215B619F5545CE55D8D37ECBB9A5FAAD28429AC27ED428490A7B11B47FAA6B96796DAFD55848933118AD4168FB489C98C1AC9FD83767431BE6FEDCAA19CEFA90A72405E9653D2B944106743FB5B23BABD841299BB37DF9DBCBF63570ABD198C16BF4E1B766F01A46C141CD87AA797B3AAC838A37A7AAEAE9261EA4D5545AC74F3B541E5D6F3D598A4948E068B98EFAC64F6B63355E284ADAF0DDACDAFB6BA761B14A08B41B32579D8EF4CC42B41732A57FB4BA7B32D43F958FDD54BEA8195377ED2324101A9858EAF592EFEC6062877153EDEC90E4293BD3E1097EF646EEBADFE0EDAAD8D927C3D92191137649C352F0EC8DA8B5BFEE7A6D316B4E5BA34649CBF3AA243929BDB1B6B434F9CB1FD8D4DC4730FDC555DABD69D69A367FDE2A6F8D76187D33DDF0DAD49AEB46DBF3B06C3816C970DA681F27758E79E69CB6B10C4EB3639665A76D087B948E47543E29F85F3523665B69B3B84A6511D8A1FC3A43D82229B41C97370AD95B4FA0634794D16EA53B18692C566D3323CE6046698C624BF8C6586CDA468E9BC1EC6930D49AC7ECE66CB1C85CA33E5E06172AA5D9416BFED705666455434C0193624F709EAA3A73BA8C4A374E1A5159453EE8C509F2C1B33A8B13B2445E02C51E666CF34FB01449E52FC37BECCFE94D9AACD30448C6E17D20E4B1C87CC1B6FE37E979C4314F6F360F6AD91824C0304976367D43DFA724A893E15F359C4D6B203227F3070CDFF3B9049F35C1ABE70AE9E32627BF0950C1BECA37BEC3E13A003076431788FB47002CC6F699E10F7885BCE7F20DBA1EA47B2244B64F2F085AC528640546DD1EFE0419F6C3A7EFFE072AB6BB4E81840000 , N'6.4.0')
END
```
7. Create a new sql file in the folder ..\src\Database\Testing and paste the sql MigrationName snippet in it.
 The filename should follow the same numbering format as the existing files.
8. Manually add the sql file created in step #7 to the ..\src\EPiServer.Marketing.Testing.Web\SchemaUpdater\Testing.zip file.
 



   



