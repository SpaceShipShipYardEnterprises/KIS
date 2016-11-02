using System;
using System.Linq;
using System.Text;

namespace KIS {

public class ModuleKISPickup : PartModule {
  [KSPField]
  public bool allowPartAttach = true;
  [KSPField]
  public bool allowStaticAttach = false;
  [KSPField]
  public bool allowPartStack = false;
  [KSPField]
  public float maxDistance = 2;
  [KSPField]
  public float grabMaxMass = 1;
  [KSPField]
  public string dropSndPath = "KIS/Sounds/drop";
  [KSPField]
  public string attachPartSndPath = "KIS/Sounds/attachPart";
  [KSPField]
  public string detachPartSndPath = "KIS/Sounds/detachPart";
  [KSPField]
  public string attachStaticSndPath = "KIS/Sounds/attachStatic";
  [KSPField]
  public string detachStaticSndPath = "KIS/Sounds/detachStatic";
  public FXGroup sndFx;

  public override string GetInfo ()
  {
    var sb = new StringBuilder();
    sb.AppendFormat("<b>Max grab mass</b>: {0:F2} T", grabMaxMass);
    sb.AppendLine();
    sb.AppendFormat("<b>Max grab distance</b>: {0:F2} M", maxDistance);
    sb.AppendLine();
    sb.AppendFormat("<b>Part attachment</b>: {0}", allowPartAttach ? "ALLOWED" : "DISALLOWED");
    sb.AppendLine();
    sb.AppendFormat("<b>Part stacking</b>: {0}", allowPartStack ? "ALLOWED" : "DISALLOWED");
    sb.AppendLine();
    sb.AppendFormat("<b>Static attachment</b>: {0}", allowStaticAttach ? "ALLOWED" : "DISALLOWED");
    sb.AppendLine();
    return sb.ToString();
  }

  public string GetModuleTitle ()
  {
    return "KISPickup";
  }
}

}  // namespace
