source = '''namespace Wellcome.Dds.Dashboard.Models.Fixtures;

public class $class$
{
    public string[] Identifiers = new[]
    {
$identifiers$
    };
}    
'''    

def make_data_class(path, name):     
    identifiers = ""
    with open(path, encoding="utf-8") as identifiers_list:
        for identifier in identifiers_list.readlines():
            if identifiers:
                identifiers += ",\r\n"
            if identifier.strip():
                identifiers += "        \"" + identifier.strip() + "\""
        

    with open(f"../../Wellcome.Dds.Dashboard/Models/Fixtures/{name}.cs", encoding="utf-8", mode='w') as c_sharp_class:
        c_sharp_class.write(source.replace("$class$", name).replace("$identifiers$", identifiers))
      

if __name__ == "__main__":
    make_data_class('born-digital/production.txt', 'BornDigitalProduction')
    make_data_class('born-digital/staging.txt', 'BornDigitalStaging')
    # don't rebuild these, edit them in C# 
    # make_data_class('digitised/production.txt', 'DigitisedProduction')
    # make_data_class('digitised/staging.txt', 'DigitisedStaging')
    
    
