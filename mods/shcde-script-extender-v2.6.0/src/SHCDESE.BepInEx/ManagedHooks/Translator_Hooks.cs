using CrusaderDE;
using SHCDESE.API;

namespace SHCDESE.ManagedHooks;

public partial class ManagedHookManager
{
    //
    // Translate
    //
    internal ManagedDetour<translate_lookUpTextDelegate> translate_lookUpText_hook;
    internal delegate string translate_lookUpTextDelegate(Translate instance, string index);
    internal string Translate_lookUpText_Hook(Translate instance, string index)
    {
        //LogHelper.Verbose($"index={index}");
        string overrideName = GameTranslateAPI.Instance.GetOverwrittenLookUpText(index);
        if (string.IsNullOrEmpty(overrideName))
        {
            string result = translate_lookUpText_hook.Trampoline(instance, index);
            //LogHelper.Verbose($"index={index}, result={result}");
            return result;
        }
        return overrideName;
    }

    internal ManagedDetour<translate_lookUpText2Delegate> translate_lookUpText2_hook;
    internal delegate string translate_lookUpText2Delegate(Translate instance, string sectionString, int index);
    internal string Translate_lookUpText2_Hook(Translate instance, string sectionString, int index)
    {
        //LogHelper.Verbose($"sectionString={sectionString}, index={index}");
        string overrideName = GameTranslateAPI.Instance.GetOverwrittenLookUpTextEx(sectionString, index);
        if (string.IsNullOrEmpty(overrideName))
        {
            string result = translate_lookUpText2_hook.Trampoline(instance, sectionString, index);
            //LogHelper.Verbose($"sectionString={sectionString}, index={index}, result={result}");
            return result;
        }
        return overrideName;
    }

    internal ManagedDetour<translate_lookUpTextExDelegate> translate_lookUpTextEx_hook;
    internal delegate string translate_lookUpTextExDelegate(Translate instance, Enums.eTextSections section, int index);
    internal string Translate_lookUpTextEx_Hook(Translate instance, Enums.eTextSections section, int index)
    {
        //LogHelper.Verbose($"sectionString={section}, index={index}");

        string overrideName = GameTranslateAPI.Instance.GetOverwrittenLookUpTextEx(section.ToString(), index);
        if (string.IsNullOrEmpty(overrideName))
        {
            string result = translate_lookUpTextEx_hook.Trampoline(instance, section, index);
            //LogHelper.Verbose($"section={section}, index={index}, result={result}");
            return result;
            //return translate_lookUpTextEx_hook.Trampoline(instance, section, index);
        }
        return overrideName;
    }
}