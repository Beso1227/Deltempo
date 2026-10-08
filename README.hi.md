<div align="center">

  <a href="https://beso1227.github.io/Deltempo/">
    <img src="docs/social-preview.png" alt="Deltempo - शुद्ध सटीकता वाला Windows क्लीनर और मेमोरी ऑप्टिमाइज़र" width="100%" />
  </a>

  <br />

  # Deltempo: ओपन-सोर्स Windows क्लीनर, ऐप अनइंस्टॉलर और मेमोरी ऑप्टिमाइज़र

  <p align="center">
    <strong>README:</strong>
    <a href="README.md">English</a> ·
    <a href="README.es.md">Español</a> ·
    <a href="README.zh-CN.md">简体中文</a> ·
    <b>हिन्दी</b> ·
    <a href="README.fr.md">Français</a> ·
    <a href="README.pt-BR.md">Português</a> ·
    <a href="README.ar.md">العربية</a> ·
    <a href="README.ru.md">Русский</a> ·
    <a href="README.ja.md">日本語</a>
  </p>

  <p><strong>Windows 10 और 11 के लिए निःशुल्क, ओपन-सोर्स डिस्क क्लीनर और RAM ऑप्टिमाइज़र। टेम्प फ़ाइलों, AppData जंक और GPU शेडर कैश से 10–40+ GB स्पेस पुनः प्राप्त करें — साथ ही गहन ऐप अनइंस्टॉलर और डुप्लिकेट फ़ाइल फ़ाइंडर भी। शून्य टेलीमेट्री, कोई विज्ञापन नहीं, कोई इंस्टॉलर नहीं।</strong></p>

  <p>
    <a href="https://github.com/Beso1227/Deltempo/releases/latest/download/Deltempo.exe"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/DOWNLOAD%20FOR%20WINDOWS-00F2B0?style=for-the-badge&label=DELTEMPO&labelColor=16212B&height=48"><img alt="Windows के लिए नवीनतम Deltempo डाउनलोड करें" src="https://img.shields.io/badge/DOWNLOAD%20FOR%20WINDOWS-00F2B0?style=for-the-badge&label=DELTEMPO&labelColor=16212B&height=48" height="48" /></picture></a>
    <a href="https://beso1227.github.io/Deltempo/"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/OFFICIAL%20WEBSITE-8B5CF6?style=for-the-badge&label=VISIT&labelColor=16212B&height=48"><img alt="Deltempo की आधिकारिक वेबसाइट पर जाएँ" src="https://img.shields.io/badge/OFFICIAL%20WEBSITE-8B5CF6?style=for-the-badge&label=VISIT&labelColor=16212B&height=48" height="48" /></picture></a>
    <a href="https://github.com/Beso1227/Deltempo/releases/latest/download/deltempo_cli.exe"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/CLI%20INSTALLER-0DD3BA?style=for-the-badge&label=HEADLESS&labelColor=16212B&height=48"><img alt="Deltempo हेडलेस CLI डाउनलोड करें" src="https://img.shields.io/badge/CLI%20INSTALLER-0DD3BA?style=for-the-badge&label=HEADLESS&labelColor=16212B&height=48" height="48" /></picture></a>
    <a href="https://github.com/Beso1227/Deltempo"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/STAR%20THE%20REPO-F59E0B?style=for-the-badge&label=GITHUB&labelColor=16212B&height=48"><img alt="GitHub पर Deltempo को स्टार करें" src="https://img.shields.io/badge/STAR%20THE%20REPO-F59E0B?style=for-the-badge&label=GITHUB&labelColor=16212B&height=48" height="48" /></picture></a>
  </p>

  <p>
    <sub>टर्मिनल पसंद है? एक ही लाइन नवीनतम रिलीज़ इंस्टॉल और सत्यापित कर देती है &mdash;
      <code>irm https://beso1227.github.io/Deltempo/win | Invoke-Expression</code>
      &middot; बग या फ़ीचर आइडिया मिला? <a href="https://github.com/Beso1227/Deltempo/issues/new/choose"><strong>issue खोलें</strong></a> &mdash; हर अनुरोध पढ़ा जाता है।
      अगर Deltempo ने आपको कुछ डिस्क स्पेस वापस दिलाया, तो एक <a href="https://github.com/Beso1227/Deltempo"><strong>स्टार</strong></a> सचमुच दूसरों को इसे खोजने में मदद करता है।</sub>
  </p>

  <p>
    <a href="https://github.com/Beso1227/Deltempo/releases/latest"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/github/v/release/Beso1227/Deltempo?style=for-the-badge&label=RELEASE&color=00F2B0&labelColor=16212B"><img alt="नवीनतम रिलीज़" src="https://img.shields.io/github/v/release/Beso1227/Deltempo?style=for-the-badge&label=RELEASE&color=00F2B0" height="28" /></picture></a>
    <a href="https://github.com/Beso1227/Deltempo/actions/workflows/ci.yml"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/github/actions/workflow/status/Beso1227/Deltempo/ci.yml?style=for-the-badge&label=CI&logo=github&labelColor=16212B"><img alt="CI बिल्ड स्थिति" src="https://img.shields.io/github/actions/workflow/status/Beso1227/Deltempo/ci.yml?style=for-the-badge&label=CI&logo=github" height="28" /></picture></a>
    <a href="docs/TESTING.md"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/tests-726_passed-00F2B0?style=for-the-badge&label=QUALITY&labelColor=16212B"><img alt="परीक्षण: 726 पास, 0 विफल" src="https://img.shields.io/badge/tests-726_passed-00F2B0?style=for-the-badge&label=QUALITY" height="28" /></picture></a>
    <a href="LICENSE"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/github/license/Beso1227/Deltempo?style=for-the-badge&label=LICENSE&color=00F2B0&logo=github&labelColor=16212B"><img alt="लाइसेंस: MIT" src="https://img.shields.io/github/license/Beso1227/Deltempo?style=for-the-badge&label=LICENSE&color=00F2B0&logo=github" height="28" /></picture></a>
  </p>

  <p>
    <a href="https://dotnet.microsoft.com/"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/.NET-10-512BD4?style=for-the-badge&label=RUNTIME&logo=dotnet&logoColor=white&labelColor=16212B"><img alt=".NET 10" src="https://img.shields.io/badge/.NET-10-512BD4?style=for-the-badge&label=RUNTIME&logo=dotnet&logoColor=white" height="28" /></picture></a>
    <a href="https://learn.microsoft.com/dotnet/csharp/"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/C%23-14-239120?style=for-the-badge&label=LANGUAGE&logo=csharp&logoColor=white&labelColor=16212B"><img alt="C# 14" src="https://img.shields.io/badge/C%23-14-239120?style=for-the-badge&label=LANGUAGE&logo=csharp&logoColor=white" height="28" /></picture></a>
    <a href="#at-a-glance"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/windows_10_%2F_11_x64-0078D4?style=for-the-badge&label=PLATFORM&logo=windows&labelColor=16212B"><img alt="प्लेटफ़ॉर्म समर्थन" src="https://img.shields.io/badge/windows_10_%2F_11_x64-0078D4?style=for-the-badge&label=PLATFORM&logo=windows" height="28" /></picture></a>
    <a href="https://github.com/Beso1227/Deltempo/releases/latest"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/portable_single--file-F59E0B?style=for-the-badge&label=BINARY&labelColor=16212B"><img alt="पोर्टेबल सिंगल-फ़ाइल" src="https://img.shields.io/badge/portable_single--file-F59E0B?style=for-the-badge&label=BINARY" height="28" /></picture></a>
  </p>

  <p>
    <a href="#privacy"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/zero_telemetry-00F2B0?style=for-the-badge&label=PRIVACY&labelColor=16212B"><img alt="शून्य टेलीमेट्री, 100% ऑफ़लाइन" src="https://img.shields.io/badge/zero_telemetry-00F2B0?style=for-the-badge&label=PRIVACY" height="28" /></picture></a>
    <a href="docs/THREAT_MODEL.md"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/STRIDE_hardened-8B5CF6?style=for-the-badge&label=SECURITY&labelColor=16212B"><img alt="STRIDE हार्डन्ड" src="https://img.shields.io/badge/STRIDE_hardened-8B5CF6?style=for-the-badge&label=SECURITY" height="28" /></picture></a>
    <a href="docs/ARCHITECTURE.md#safety-pipeline"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/two--phase_verified-8B5CF6?style=for-the-badge&label=SAFETY&labelColor=16212B"><img alt="दो-चरण सत्यापित सुरक्षा" src="https://img.shields.io/badge/two--phase_verified-8B5CF6?style=for-the-badge&label=SAFETY" height="28" /></picture></a>
    <a href="docs/ARCHITECTURE.md"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/NT_kernel_native-0DD3BA?style=for-the-badge&label=MEMORY&labelColor=16212B"><img alt="NT कर्नल नेटिव मेमोरी इंजन" src="https://img.shields.io/badge/NT_kernel_native-0DD3BA?style=for-the-badge&label=MEMORY" height="28" /></picture></a>
  </p>

  <p>
    <a href="#quick-start"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/one--line_install-00F2B0?style=for-the-badge&label=SETUP&labelColor=16212B"><img alt="एक-लाइन इंस्टॉल" src="https://img.shields.io/badge/one--line_install-00F2B0?style=for-the-badge&label=SETUP" height="28" /></picture></a>
    <a href="https://github.com/Beso1227/Deltempo/releases"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/github/downloads/Beso1227/Deltempo/total?style=for-the-badge&label=DOWNLOADS&color=00F2B0&logo=github&labelColor=16212B"><img alt="GitHub डाउनलोड" src="https://img.shields.io/github/downloads/Beso1227/Deltempo/total?style=for-the-badge&label=DOWNLOADS&color=00F2B0&logo=github" height="28" /></picture></a>
    <a href="https://github.com/Beso1227/Deltempo"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/github/stars/Beso1227/Deltempo?style=for-the-badge&label=STARS&color=F59E0B&logo=github&labelColor=16212B"><img alt="GitHub स्टार्स" src="https://img.shields.io/github/stars/Beso1227/Deltempo?style=for-the-badge&label=STARS&color=F59E0B&logo=github" height="28" /></picture></a>
    <a href="https://github.com/Beso1227/Deltempo/pulls"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/PRs_welcome-00F2B0?style=for-the-badge&label=CONTRIBUTING&labelColor=16212B"><img alt="PR स्वागत है" src="https://img.shields.io/badge/PRs_welcome-00F2B0?style=for-the-badge&label=CONTRIBUTING" height="28" /></picture></a>
  </p>

  <p>
    <sub>
      <a href="docs/ARCHITECTURE.md">आर्किटेक्चर</a> &middot;
      <a href="docs/THREAT_MODEL.md">थ्रेट मॉडल</a> &middot;
      <a href="docs/TESTING.md">टेस्टिंग गाइड</a> &middot;
      <a href="docs/BENCHMARKS.md">बेंचमार्क</a> &middot;
      <a href="docs/RELEASES.md">रिलीज़ इंजीनियरिंग</a> &middot;
      <a href="SECURITY.md">सुरक्षा नीति</a> &middot;
      <a href="#quick-start">त्वरित आरंभ</a>
    </sub>
  </p>

  <br />

  <video src="docs/deltempo.mp4" poster="docs/deltempo.webp" controls preload="metadata" width="100%"></video>

</div>

---

  <a id="at-a-glance"></a>

## एक नज़र में

| गुण | विवरण |
| :--- | :--- |
| **आधिकारिक वेबसाइट** | [beso1227.github.io/Deltempo](https://beso1227.github.io/Deltempo/) |
| **प्लेटफ़ॉर्म** | Windows 10 और 11 (64-बिट / x64) |
| **संस्करण** | v3.0.0 (उत्पादन रिलीज़) |
| **लाइसेंस** | ओपन-सोर्स ([MIT](LICENSE)) |
| **इंटरफ़ेस** | आधुनिक डेस्कटॉप GUI (WPF Fluent) और हेडलेस टर्मिनल CLI |
| **वितरण** | पोर्टेबल सिंगल-फ़ाइल एक्ज़ीक्यूटेबल (स्व-निहित, इंस्टॉलर की आवश्यकता नहीं) |
| **परीक्षण कवरेज** | 726 स्वचालित परीक्षण (100% पास दर, 0 विफल, 0 छोड़े गए), प्रतिस्पर्धी फ़ाइलसिस्टम फ़ज़िंग |
| **CLI उपलब्धता** | `deltempo` कमांड पहली GUI लॉन्च पर स्वयं इंस्टॉल हो जाता है — कोई मैनुअल सेटअप नहीं |
| **टेलीमेट्री** | शून्य टेलीमेट्री। स्कैन, सफ़ाई, मेमोरी और अनइंस्टॉलर संचालन 100% ऑफ़लाइन निष्पादित होते हैं |
| **सुरक्षा इंजन** | दो-चरण योजना (`SCAN → PLAN → PROTECT → REVALIDATE → CLEAN`) — 5 जोखिम स्तर और लेन-देन जर्नलिंग के साथ |
| **ऐप अनइंस्टॉलर** | बल्क साइलेंट अनइंस्टॉलर, BCU इंजन, अवशिष्ट AppData/रजिस्ट्री ट्रेस सफ़ाई, टूटे हुए ऐप्स के लिए ज़बरदस्ती वाइप |
| **रिस्टोर पॉइंट्स** | वैकल्पिक अनइंस्टॉल-पूर्व Windows सिस्टम रिस्टोर पॉइंट्स (उपयोगकर्ता-नियंत्रित, डिफ़ॉल्ट रूप से अक्षम) |
| **सेवा इंटेलिजेंस** | बताता है: *"क्या इसे अक्षम करने से कुछ गड़बड़ होगा?"* — 3 सुरक्षा निर्णयों, ऑफ़लाइन ह्यूरिस्टिक्स और मल्टी-मॉडल AI के माध्यम से |
| **प्रोसेस मैनेजर** | लाइव हाई-DPI ऐप आइकन एक्सट्रैक्शन और मेमोरी फ़ुटप्रिंट विश्लेषण के साथ रीयल-टाइम प्रोसेस सूची |
| **WinUtil इंटीग्रेशन** | टूलबार से सीधे Chris Titus Tech WinUtil (CTT) यूटिलिटी का 1-क्लिक लॉन्चर |
| **मेमोरी इंजन** | नेटिव Windows NT कर्नल कॉल्स (`NtSetSystemInformation`, `EmptyWorkingSet`) |
| **प्राथमिकता हब** | श्रेणीबद्ध 4-टैब नियंत्रण केंद्र (*अपडेट*, *सामान्य*, *मेमोरी*, *स्टोरेज और सुरक्षा*) |
| **ट्रे गार्जियन** | हाई-DPI नेटिव Win32 आइकन (`LoadCrispTrayIcon`) — लाइव RAM टेलीमेट्री और 1-क्लिक बूस्ट के साथ |
| **थीम और सुलभता** | प्रो आँख-आराम Porcelain Slate लाइट मोड, Obsidian डार्क मोड, और पूर्ण Arabic RTL लेआउट सुरक्षा |
| **अपडेट** | क्रिप्टोग्राफ़िक रूप से सत्यापित (SHA-256) स्थिर और बीटा रिलीज़ चैनल — एटॉमिक स्टेजिंग के साथ |

---

## Deltempo क्या है?

**Deltempo** एक आधुनिक, उच्च-प्रदर्शन, ओपन-सोर्स Windows रखरखाव सूट है, जो सुरक्षित रूप से स्टोरेज स्पेस पुनः प्राप्त करने, ज़िद्दी सॉफ़्टवेयर को पूरी तरह अनइंस्टॉल करने, स्टार्टअप बूट प्रभाव की निगरानी करने और सिस्टम मेमोरी को ऑप्टिमाइज़ करने के लिए बनाया गया है। यह निजी दस्तावेज़ों, ब्राउज़र क्रेडेंशियल्स या महत्वपूर्ण ऑपरेटिंग सिस्टम घटकों को छुए बिना, निष्कासन-योग्य एप्लिकेशन कैश, अनाथ इंस्टॉलर अवशेष, बिल्ड आर्टिफ़ैक्ट और पुराने सिस्टम लॉग्स को साफ़ करता है।

पारंपरिक क्लीनअप उपयोगिताएँ अक्सर अपारदर्शी ब्लैक बॉक्स की तरह काम करती हैं, बंडल किए गए एडवेयर इंस्टॉल करती हैं, या गहरी रजिस्ट्री अवशेष छोड़ देती हैं। Deltempo को **सुरक्षा-प्रथम आर्किटेक्चर** पर बनाया गया है: उम्मीदवार पाथ को स्पष्ट जोखिम स्तरों में वर्गीकृत किया जाता है, हटाने से पहले सिमुलेट किया जाता है, अधिकृत डायरेक्टरी रूट्स के भीतर सीमित रखा जाता है, और फ़ाइलसिस्टम रेस कंडीशन से बचने के लिए हटाने से ठीक पहले पुनः मान्य किया जाता है।

डिस्क सफ़ाई के अलावा, Deltempo में निम्न-स्तरीय Windows NT कर्नल मेमोरी प्रबंधन टूल शामिल हैं, जो आधिकारिक Win32 और NT सिस्टम कॉल्स के माध्यम से स्टैंडबाय पेज लिस्ट फ़्लश करते हैं और निष्क्रिय वर्किंग सेट्स को ट्रिम करते हैं।

---

## ⚔️ Deltempo प्रतिस्पर्धियों से कैसे तुलना करता है

अधिकांश Windows क्लीनिंग और ऑप्टिमाइज़ेशन उपयोगिताएँ या तो वाणिज्यिक एडवेयर बंडल करती हैं, घुसपैठ वाली बैकग्राउंड सेवाओं की आवश्यकता रखती हैं, आवश्यक सुविधाओं को सशुल्क सदस्यता के पीछे लॉक कर देती हैं, या पुराने कोडबेस पर निर्भर रहती हैं।

Deltempo पूर्णतः ओपन-सोर्स है, इसमें शून्य टेलीमेट्री है, किसी इंस्टॉलेशन की आवश्यकता नहीं है, और यह सटीक सफ़ाई, डीप-रूट सॉफ़्टवेयर अनइंस्टॉलेशन, कर्नल-स्तरीय मेमोरी प्रबंधन और स्टार्टअप सेवा इंटेलिजेंस को जोड़ने वाला एक एंड-टू-एंड सूट प्रदान करता है।

| क्षमता / सुविधा | **Deltempo** | **CCleaner** | **BleachBit** | **Bulk Crap Uninstaller** | **Windows स्टोरेज सेंस** |
| :--- | :---: | :---: | :---: | :---: | :---: |
| **लाइसेंस और कोडबेस** | **MIT ओपन-सोर्स (C# 14 / .NET 10)** | प्रोप्राइटरी / वाणिज्यिक | GPLv3 ओपन-सोर्स (Python/GTK) | Apache 2.0 ओपन-सोर्स (.NET) | प्रोप्राइटरी (Microsoft) |
| **टेलीमेट्री और गोपनीयता** | **शून्य टेलीमेट्री (100% ऑफ़लाइन)** | ⚠️ ट्रैकर और डेटा संग्रह | ✅ शून्य टेलीमेट्री | न्यूनतम टेलीमेट्री | Windows डायग्नोस्टिक टेलीमेट्री |
| **बंडल एडवेयर / अपसेल** | **कोई नहीं / कभी नहीं** | ⚠️ ऐतिहासिक एडवेयर बंडल और अपसेल | कोई नहीं | कोई नहीं | कोई नहीं |
| **मेमोरी इंजन** | **नेटिव NT कर्नल (`NtSetSystemInformation`)** | ⚠️ बुनियादी (केवल सशुल्क Pro) | ❌ कोई नहीं | ❌ कोई नहीं | ❌ कोई नहीं |
| **डीप-रूट ऐप अनइंस्टॉलर** | **साइलेंट बल्क + अवशिष्ट उन्मूलन** | ⚠️ बुनियादी (गहन के लिए सशुल्क Pro) | ❌ कोई नहीं | ✅ व्यापक | ❌ केवल बुनियादी जोड़ें/हटाएँ |
| **वैकल्पिक रिस्टोर पॉइंट्स** | **वैकल्पिक (उपयोगकर्ता की पसंद, डिफ़ॉल्ट रूप से बंद)** | ⚠️ स्वचालित / सशुल्क सुविधा | ❌ कोई नहीं | वैकल्पिक | मैनुअल सिस्टम टॉगल |
| **स्टार्टअप सेवा इंटेलिजेंस** | **हाँ ("क्या कुछ टूट जाएगा?" 3-स्तरीय निर्णय)** | ❌ सादा चालू/बंद टॉगल सूची | ❌ कोई नहीं | विस्तृत रजिस्ट्री सूची | बुनियादी टास्क मैनेजर मेट्रिक्स |
| **अवशिष्ट ट्रेस स्वीपर** | **AppData, ProgramData, रजिस्ट्री और शॉर्टकट** | ⚠️ केवल सशुल्क Pro | ❌ कोई नहीं | ✅ मैनुअल रजिस्ट्री खोज | ❌ कोई नहीं |
| **डेवलपर और शेडर कैश** | **NuGet, npm, pip, Cargo, Gradle, GPU शेडर्स** | ❌ केवल ब्राउज़र और Windows | ⚠️ आंशिक | ❌ कोई नहीं | ❌ कोई नहीं |
| **बड़ी फ़ाइलें खोजना** | **AI-वर्गीकृत (>50MB, जोखिम-वर्गीकृत)** | ❌ बुनियादी फ़ाइल खोज | ❌ कोई नहीं | ❌ कोई नहीं | बुनियादी ड्राइव विभाजन |
| **सिस्टम फ़ाइल मरम्मत** | **इंटीग्रेटेड SFC, DISM और CHKDSK** | ❌ अलग सशुल्क यूटिलिटी | ❌ कोई नहीं | ❌ कोई नहीं | मैनुअल कमांड प्रॉम्प्ट |
| **WinUtil (Chris Titus) इंटीग्रेशन** | **1-क्लिक इंटीग्रेटेड एलिवेटेड लॉन्चर** | ❌ कोई नहीं | ❌ कोई नहीं | ❌ कोई नहीं | ❌ कोई नहीं |
| **आधुनिक UI और आँख आराम** | **Obsidian डार्क और Porcelain Slate लाइट (WPF)** | पुराना / अव्यवस्थित | पुराना GTK2/3 इंटरफ़ेस | पुराना WinForms इंटरफ़ेस | Windows सेटिंग्स में निर्मित |
| **पूर्ण CLI समानता** | **हाँ (`deltempo` CLI — `--json` और dry-run के साथ)** | ⚠️ सीमित कमांड स्विच | बुनियादी CLI | बुनियादी CLI | ❌ कोई नहीं |
| **वितरण** | **पोर्टेबल सिंगल-फ़ाइल (68 MB, बिना इंस्टॉलर)** | सेटअप इंस्टॉलर और सेवाओं की आवश्यकता | इंस्टॉलर या पोर्टेबल zip | इंस्टॉलर और रनटाइम की आवश्यकता | OS में निर्मित |

---

## 🌟 मुख्य सुविधाओं की विस्तृत झलक

### 1. 🧹 सटीक 26+ स्कोप स्टोरेज क्लीनर

Deltempo सिस्टम, डेवलपर और गेमिंग वातावरण में निष्कासन-योग्य डेटा को लक्षित करता है, बिना उपयोगकर्ता दस्तावेज़ों, सक्रिय प्रमाणीकरण टोकन या व्यक्तिगत सेटिंग्स को छुए:

* **ऑपरेटिंग सिस्टम स्कोप**: यूज़र टेम्प (`%TEMP%`), Windows टेम्प (`C:\Windows\Temp`), प्रीफ़ेच, Windows अपडेट डाउनलोड कैश (`SoftwareDistribution\Download`), Windows अपग्रेड अवशेष (`$WINDOWS.~BT`), डिलीवरी ऑप्टिमाइज़ेशन कैश, Windows एरर रिपोर्टिंग (`WER`), मेमोरी डंप, और फ़ॉन्ट/थंबनेल कैश।
* **GPU और गेम शेडर्स**: NVIDIA App / GeForce Experience OTA कैश, AMD Radeon Software कैश, DirectX शेडर कैश (`D3DSCache`), Vulkan पाइपलाइन (`GLCache`), Steam शेडर प्री-कैशिंग, और Epic Games लॉन्चर वेबकैश।
* **डेवलपर इकोसिस्टम**: NuGet v3 लोकल कैश, npm कैश, pip कैश, Rust Cargo टारगेट कैश, Gradle कैश, Android Studio एमुलेटर अस्थायी स्नैपशॉट, और VS Code एक्सटेंशन कैश।
* **आधुनिक ब्राउज़र और संचार**: Chromium प्रोफ़ाइल (Chrome, Edge, Brave, Opera, Vivaldi, Arc) और Gecko प्रोफ़ाइल (Firefox) के निष्कासन-योग्य कैश डायरेक्टरी — **लॉगिन सत्र पूरी तरह सुरक्षित रहते हैं**; Discord, Slack और Spotify मीडिया कैश।
* **सेफ़टी शील्ड (>24 घंटे)**: वैकल्पिक सुरक्षा गार्ड जो पिछले 24 घंटों में बनी या संशोधित किसी भी फ़ाइल को छूट देता है, ताकि सक्रिय बैकग्राउंड इंस्टॉलर या चल रहे एडिटर से टकराव न हो।
* **TOCTOU गार्ड**: अनलिंक करने से ठीक पहले फ़ाइल सीमाओं, विशेषताओं और कैनॉनिकल पाथ की पुष्टि करके रेस कंडीशन को समाप्त करता है।

---

### 2. 📦 डीप-रूट ऐप अनइंस्टॉलर और अवशिष्ट स्वीपर

ज़िद्दी ब्लोटवेयर, आधा-हटाया गया सॉफ़्टवेयर और अव्यवस्थित अनइंस्टॉलेशन विज़ार्ड से अलविदा कहें:

* **एकीकृत एप्लिकेशन इन्वेंटरी**: 64-बिट और 32-बिट रजिस्ट्री हाइव्स (`HKLM`, `HKCU`) के साथ-साथ आधुनिक Windows Store (AppX/UWP) पैकेज स्कैन करती है। वास्तविक इंस्टॉल आकार, प्रकाशक सत्यापन, संस्करण और इंस्टॉल तिथियाँ प्रदर्शित करती है।
* **वैकल्पिक सिस्टम रिस्टोर पॉइंट**: अन्य उपयोगिताओं के विपरीत जो धीमे 2-मिनट के सिस्टम रिस्टोर पॉइंट को बाध्य करती हैं या पूरी तरह छोड़ देती हैं, Deltempo पूर्ण नियंत्रण उपयोगकर्ता को देता है। एक समर्पित टॉगल आपको तय करने देता है कि अनइंस्टॉल-पूर्व रिस्टोर चेकपॉइंट बनाना है या नहीं (**डिफ़ॉल्ट रूप से बंद**)।
* **साइलेंट मल्टी-ऐप बल्क हटाना**: कई एप्लिकेशन चुनें और दर्जनों बार-बार आने वाले इंस्टॉलर डायलॉग पर क्लिक किए बिना बिना ध्यान दिए अनइंस्टॉलेशन शुरू करें।
* **डीप-रूट अवशिष्ट स्वीप**: किसी एप्लिकेशन के अनइंस्टॉलर के समाप्त होने के बाद, Deltempo का स्कैनर अनाथ अवशेषों को खोजता है:
  * रजिस्ट्री शाखाएँ: `HKCU\Software\<Vendor>`, `HKLM\Software\<Vendor>`, `HKLM\Software\WOW6432Node\<Vendor>`.
  * फ़ाइलसिस्टम डायरेक्टरी: `%LocalAppData%\<App>`, `%AppData%\<App>`, `%ProgramData%\<App>`, `%ProgramFiles%\<App>`.
  * स्टार्टअप एंट्री और स्टार्ट मेन्यू के अनाथ शॉर्टकट।
* **भ्रष्ट सॉफ़्टवेयर के लिए ज़बरदस्ती वाइप**: यदि कोई अनइंस्टॉलर टूटा हुआ, ग़ायब है या त्रुटियाँ फेंकता है, तो Deltempo सभी संबंधित फ़ाइलसिस्टम डायरेक्टरियों को ज़बरदस्ती साफ़ करता है और उसके रजिस्ट्री कुंजियों को स्वच्छ रूप से डिरजिस्टर करता है।

---

### 3. ⚡ नेटिव Windows NT कर्नल मेमोरी ऑप्टिमाइज़र

उपभोक्ता "RAM क्लीनर" के विपरीत जो सिर्फ़ मेमोरी को स्वैप फ़ाइल में धकेलकर आपके PC को धीमा कर देते हैं, Deltempo मूल, दस्तावेज़ीकृत Windows NT कर्नल सिस्टम कॉल्स का उपयोग करता है:

* **स्टैंडबाय लिस्ट अमान्यीकरण**: उच्च-मांग वाले कार्यों (गेमिंग, कंपाइलिंग, रेंडरिंग) के लिए अप्रयुक्त कैश किए गए स्टैंडबाय मेमोरी पेजों को उपलब्ध पूल में वापस भेजने के लिए `SystemMemoryListInformation` (class `80`) के साथ `NtSetSystemInformation` कॉल करता है।
* **निष्क्रिय वर्किंग सेट ट्रिमिंग**: निष्क्रिय बैकग्राउंड प्रोसेस से त्यागे गए वर्किंग सेट्स को मुक्त करने के लिए उन्नत प्रोसेस टोकन्स (`SeProfileSingleProcessPrivilege` और `SeDebugPrivilege`) के साथ `EmptyWorkingSet` का उपयोग करता है।
* **क्रिटिकल प्रोसेस शील्ड**: मुख्य Windows घटक (`csrss.exe`, `dwm.exe`, `explorer.exe`, `lsass.exe`, `services.exe`, `smss.exe`, `svchost.exe`, और Windows Defender) स्वचालित रूप से सुरक्षित रहते हैं और कभी ट्रिम नहीं होते।
* **बैकग्राउंड ऑटो-बूस्ट**: बैकग्राउंड में मेमोरी दबाव की निगरानी कर सकता है और भौतिक RAM उपयोग उपयोगकर्ता-निर्धारित सीमा (जैसे, 85%) से अधिक होने पर स्वचालित रूप से सफ़ाई ट्रिगर कर सकता है।

---

### 4. 🧠 स्टार्टअप मैनेजर और सेवा इंटेलिजेंस

यह सोचना बंद करें कि कौन-से प्रोग्राम आपके PC के बूट समय को धीमा कर रहे हैं:

* **"क्या इसे अक्षम करने से कुछ गड़बड़ होगा?"**: प्रत्येक स्टार्टअप एप्लिकेशन और बैकग्राउंड सेवा का विश्लेषण बुद्धिमान 3-स्तरीय निर्णय बैज के साथ किया जाता है:
  * 🟢 **SafeToDisable**: सुविधा लॉन्चर, गेम अपडेटर और संचार ऐप्स जिन्हें Windows के साथ बूट होने की आवश्यकता नहीं है।
  * 🟡 **CautionNeeded**: ऑडियो कंट्रोल पैनल, टैचपैड यूटिलिटी या परिधि सॉफ़्टवेयर जहाँ हॉटकी या ट्रे मेन्यू तब तक निष्क्रिय रह सकते हैं जब तक मैन्युअल रूप से खोले न जाएँ।
  * 🔴 **EssentialKeep**: सुरक्षा सूट, क्लाउड सिंक बैकअप एजेंट या आवश्यक हार्डवेयर ड्राइवर।
* **दोहरी इंटेलिजेंस पाइपलाइन**:
  * **ऑफ़लाइन ह्यूरिस्टिक्स**: डिजिटल हस्ताक्षर, सत्यापित विक्रेता पहचान, बाइनरी पाथ और ज्ञात प्रोसेस डेटाबेस के आधार पर तत्काल निर्धारण वर्गीकरण।
  * **वैकल्पिक मल्टी-प्रोवाइडर AI**: माँग पर विस्तृत संचालन सारांश — OpenAI, Anthropic Claude, Google Gemini, Groq, OpenRouter और स्थानीय ऑफ़लाइन मॉडल (Ollama, LM Studio) का समर्थन।
* **100% प्रतिवर्ती रजिस्ट्री टॉगल**: अक्षम आइटम `Run_Deltempo_Disabled` रजिस्ट्री कुंजियों में सुरक्षित रूप से संग्रहित होते हैं। किसी भी आइटम को एक क्लिक में फिर से सक्षम किया जा सकता है।

---

### 5. 🔍 AI-वर्गीकृत बड़ी फ़ाइल निरीक्षक

पता करें कि वास्तव में आपका डिस्क स्पेस क्या खा रहा है:

* **मल्टी-ड्राइव स्कैनिंग**: `C:\` या किसी भी द्वितीयक स्थिर ड्राइव को अनुकूलन योग्य आकार सीमाओं (>50 MB, >100 MB, >500 MB, >1 GB) से अधिक फ़ाइलों के लिए तेज़ी से स्कैन करें।
* **स्वचालित श्रेणी टैगिंग**: खोजों को बुद्धिमानी से समूहों में व्यवस्थित करती है — आर्काइव (`.zip`, `.rar`, `.7z`), डिस्क इमेज (`.iso`, `.vhd`), वर्चुअल मशीन डिस्क (`.vmdk`, `.vhdx`), इंस्टॉलर (`.msi`, `.exe`), वीडियो/ऑडियो मीडिया, और पुराने लॉग फ़ाइलें।
* **सुरक्षा जोखिम स्तरीकरण**: आपके छूने से पहले प्रत्येक बड़ी फ़ाइल का सुरक्षा मूल्यांकन किया जाता है, जिससे हाइपरवाइज़र डिस्क या महत्वपूर्ण गेम इंस्टॉलेशन को ग़लती से हटाने से रोका जाता है।

---

### 6. 🛠️ Windows सिस्टम मरम्मत और CTT WinUtil इंटीग्रेशन

इंटरफ़ेस से सीधे Windows ऑपरेटिंग सिस्टम करप्शन का निदान और मरम्मत करें:

* **SFC (सिस्टम फ़ाइल चेकर)**: भ्रष्ट सिस्टम फ़ाइलों की मरम्मत के लिए उन्नत संदर्भ में `sfc /scannow` निष्पादित करता है।
* **DISM सर्विसिंग**: Windows कंपोनेंट स्टोर स्वास्थ्य की जाँच, स्कैन और पुनर्स्थापना करता है (`/Cleanup-Image /RestoreHealth`)।
* **WinSxS बेस रीसेट**: बड़े Windows अपडेट के बाद गीगाबाइट पुनः प्राप्त करने के लिए पदनिर्णित कंपोनेंट स्टोर संस्करणों को साफ़ करता है।
* **CHKDSK और नेटवर्क रीसेट**: अगली बूट पर डिस्क वॉल्यूम सत्यापन शेड्यूल करें या एक क्लिक में DNS फ़्लश करें और Winsock स्टैक रीसेट करें।
* **Chris Titus Tech WinUtil (CTT)**: इंटीग्रेटेड 1-क्लिक लॉन्चर प्रख्यात एलिवेटेड PowerShell WinUtil सूट चलाता है — डिब्लोटिंग, टेलीमेट्री हटाने और स्वचालित winget सॉफ़्टवेयर सेटअप के लिए।

---

### 7. 📊 रीयल-टाइम प्रोसेस मैनेजर

* **नेटिव हाई-DPI आइकन एक्सट्रैक्शन**: नेटिव Win32 `SHGetFileInfo` और `ExtractIconEx` रूटीन्स का उपयोग करके 32-बिट स्पष्ट एक्ज़ीक्यूटेबल आइकन का लाइव एक्सट्रैक्शन।
* **मेमोरी और PID टेलीमेट्री**: रीयल-टाइम प्रोसेस मेमोरी फ़ुटप्रिंट, प्रोसेस ID, प्रकाशक जानकारी और फ़ाइल पाथ।
* **सुरक्षित समाप्ति**: संरक्षित किल रूटीन महत्वपूर्ण Windows सिस्टम प्रोसेस की दुर्घटनावश समाप्ति को रोकते हैं।

---

### 8. 🎨 प्रो आँख-आराम थीम और बहुभाषी RTL समर्थन

उपयोगकर्ता अनुभव के प्रति जुनूनी ध्यान के साथ डिज़ाइन:

* **प्रो आँख-आराम लाइट मोड**: एक सुकून देने वाला, Fluent/macOS पोर्सलेन और स्लेट थीम (`#F1F5F9`) जो आँखों के तनाव को पूरी तरह समाप्त करता है, चमकदार सफ़ेद स्क्रीन की जगह लेता है, और उच्च-कंट्रास्ट ओशन एज़्योर (`#0284C7`) एक्सेंट का उपयोग करता है।
* **Obsidian डार्क मोड**: जीवंत इलेक्ट्रिक सियान एक्सेंट, सूक्ष्म ग्लासमॉर्फ़िज़्म और डबल-बेज़ल कार्ड के साथ चिकना, डीप-स्पेस डार्क थीम।
* **व्यापक बहुभाषी कवरेज**: **अंग्रेज़ी, अरबी, स्पैनिश, फ़्रेंच और जर्मन** में पूर्ण नेटिव अनुवाद।
* **RTL लेआउट और संख्या सुरक्षा**: अरबी मोड वास्तविक दाएँ-से-बाएँ (RTL) विंडो प्रवाह सक्रिय करता है, जबकि मेट्रिक्स, पाथ और प्रगति संकेतकों पर बाएँ-से-दाएँ फ़ॉर्मैटिंग को कड़ाई से लागू करता है (`0.0 MB`, `32%`, `C:\...`) ताकि संख्याएँ और स्टोरेज आँकड़े कभी उलटे या विकृत न हों।

---

### 9. 🔔 पिक्सेल-परफ़ेक्ट सिस्टम ट्रे गार्जियन

* **सच्चा हाई-DPI Win32 आइकन (`LoadCrispTrayIcon`)**: 32-बिट ARGB अल्फ़ा पारदर्शिता के साथ प्रत्यक्ष Win32 GDI आइकन निर्माण (`CreateIconIndirect`) का उपयोग करता है, जो 100%, 125%, 150%, 175% और 200%+ स्केलिंग डिस्प्ले पर बिना धुंधलापन के तीखा रेंडरिंग देता है।
* **लाइव होवर टेलीमेट्री**: ट्रे टूलटिप में रीयल-टाइम मेमोरी उपयोग प्रदर्शित करता है: `RAM: 42% (13.4 GB / 31.9 GB)`।
* **त्वरित कॉन्टेक्स्ट कार्रवाइयाँ**: मुख्य विंडो खोले बिना **1-क्लिक बूस्ट मेमोरी** या **त्वरित स्मार्ट क्लीन** ट्रिगर करने के लिए राइट-क्लिक करें।
* **Explorer लचीलापन**: यदि `explorer.exe` पुनरारंभ होता है तो आइकन को स्वचालित रूप से बहाल करने के लिए Windows `TaskbarCreated` प्रसारण संदेश को सुनता है।

---

## 💻 CLI संदर्भिका और हेडलेस ऑटोमेशन

Deltempo में उच्च-प्रदर्शन, स्क्रिप्ट-योग्य CLI (`deltempo_cli.exe` या `deltempo` कमांड) शामिल है, जिसे शेड्यूल किए गए कार्यों, सिस्टम व्यवस्थापकों और हेडलेस वातावरण के लिए डिज़ाइन किया गया है।

```powershell
# फ़ाइलें हटाए बिना साफ़ करने योग्य लक्ष्यों का पूर्वावलोकन करें (dry-run)
deltempo clean --dry-run

# सुरक्षित सफ़ाई चलाएँ और हटाई गई फ़ाइलों को Windows रीसाइकल बिन में भेजें
deltempo clean --safe --recycle-bin

# स्टैंडबाय RAM फ़्लश करें और प्रोसेस वर्किंग सेट्स ट्रिम करें
deltempo boost

# बिना रिस्टोर पॉइंट बनाए किसी एप्लिकेशन का गहन अनइंस्टॉलेशन
deltempo uninstall "Google Chrome" --silent --force

# अनइंस्टॉलेशन के दौरान वैकल्पिक रिस्टोर पॉइंट निर्माण
deltempo uninstall "Epic Games Launcher" --restore-point

# संरचित JSON में सिस्टम टेलीमेट्री और मेमोरी स्वास्थ्य आउटपुट करें
deltempo status --json
```

### CLI कमांड सारांश

| कमांड | उद्देश्य | प्रमुख फ़्लैग और विकल्प |
| :--- | :--- | :--- |
| `deltempo scan [filter]` | निष्कासन-योग्य डेटा के लिए लक्ष्य स्कैन करें | `--json`, `--silent` |
| `deltempo clean [filter]` | निष्कासन-योग्य कैश साफ़ करें | `--dry-run`, `--recycle-bin`, `--safe`, `--all`, `--json`, `--yes` |
| `deltempo smart-clean` | सत्यापित सुरक्षित कैश की त्वरित सफ़ाई | `--dry-run`, `--json`, `--yes` |
| `deltempo deep-clean` | स्वायत्त पूर्ण सफ़ाई (RAM, DISM, स्कोप) | `--dry-run`, `--json`, `--yes` |
| `deltempo boost` | NT कर्नल के माध्यम से सिस्टम मेमोरी ऑप्टिमाइज़ करें | `--all`, `--standby`, `--cache`, `--workingsets`, `--json` |
| `deltempo uninstall <app>` | डीप-रूट अनइंस्टॉलेशन और अवशिष्ट सफ़ाई | `--dry-run`, `--force`, `--silent`, `--restore-point`, `--json` |
| `deltempo large [path]` | स्पेस खपत करने वाली फ़ाइलों के लिए ड्राइव स्कैन करें | `--min <size>`, `--type <cat>`, `--safe`, `--top <n>`, `--sort <size\|date>` |
| `deltempo large inspect <file>` | फ़ाइल का जोखिम स्तर और सुरक्षा निर्णय जाँचें | `--json` |
| `deltempo large clean` | निष्कासन-योग्य बड़ी फ़ाइलों को रीसाइकल करें | `--dry-run`, `--yes`, `--safe-only` |
| `deltempo startup [list]` | स्टार्टअप एप्लिकेशन और बूट प्रभाव जाँचें | `--high`, `--json` |
| `deltempo startup disable <app>` | स्टार्टअप प्रोग्राम को प्रतिवर्ती रूप से अक्षम करें | N/A |
| `deltempo startup enable <app>` | अक्षम किए गए स्टार्टअप प्रोग्राम को बहाल करें | N/A |
| `deltempo repair [subcommand]` | Windows अखंडता जाँच और सर्विसिंग मरम्मत | `sfc`, `dism`, `winsxs`, `chkdsk`, `update`, `network` |
| `deltempo status` | सिस्टम टेलीमेट्री और मेमोरी जानकारी प्रदर्शित करें | `--json` |
| `deltempo test` | इंजन की स्व-जाँच (मेमोरी API, खोजे गए स्कोप) | N/A |
| `deltempo help` | पूर्ण कमांड संदर्भिका प्रिंट करें | N/A |
| `deltempo update [check]` | आधिकारिक रिलीज़ जाँचें या अपडेट लागू करें | `check`, `--dry-run` |
| `deltempo register` | `deltempo` कमांड स्वयं इंस्टॉल / जाँचें | `--status`, `--remove` |
| `deltempo unregister` | `register` द्वारा बनाए गए हर आर्टिफ़ैक्ट को हटाएँ | N/A |

---

  <a id="quick-start"></a>

## 🚀 त्वरित आरंभ

> ### ⚡ एक लाइन में चलाएँ
>
> ```powershell
> irm https://beso1227.github.io/Deltempo/win | Invoke-Expression
> ```
>
> नवीनतम रिलीज़ डाउनलोड करता है, उसका SHA-256 सत्यापित करता है, उसे `%LOCALAPPDATA%\Deltempo\bin` में कैश करता है, और लॉन्च करता है।
> हेडलेस कंसोल बाइनरी पसंद है? `win` को [`win-cli`](https://beso1227.github.io/Deltempo/) से बदलें।
>
> ### 💻 …और CLI भी तैयार है
>
> पहली लॉन्च आपके लिए `deltempo` कमांड इंस्टॉल कर देती है — `cmd.exe`, PowerShell और <kbd>Win</kbd>+<kbd>R</kbd> तीनों काम करते हैं।
> बाद में एक **नई** टर्मिनल विंडो खोलें, फिर:
>
> ```powershell
> deltempo test        # इंजन की स्व-जाँच
> deltempo status      # लाइव डिस्क + RAM टेलीमेट्री
> deltempo register --status
> ```

### विकल्प 1: पोर्टेबल स्टैंडअलोन एक्ज़ीक्यूटेबल (अनुशंसित)

1. [Latest Release](https://github.com/Beso1227/Deltempo/releases/latest) पेज से **`Deltempo.exe`** डाउनलोड करें।
2. `Deltempo.exe` सीधे चलाएँ (किसी इंस्टॉलर की आवश्यकता नहीं, स्व-निहित सिंगल-फ़ाइल)।
3. स्पेस पुनः प्राप्त करने के लिए **Scan Now** या **1-Click Deep Clean** पर क्लिक करें।

### विकल्प 2: टर्मिनल वन-लाइनर (PowerShell)

अपने टर्मिनल से सीधे नवीनतम रिलीज़ लॉन्च करें — कोई ब्राउज़र नहीं, कोई इंस्टॉलर नहीं:

```powershell
irm https://beso1227.github.io/Deltempo/win | Invoke-Expression
```

बूटस्ट्रैप स्क्रिप्ट नवीनतम `Deltempo.exe` डाउनलोड करती है, उसे प्रकाशित `checksums.sha256` के विरुद्ध उसका SHA-256 सत्यापित करती है, उसे `%LOCALAPPDATA%\Deltempo\bin` में कैश करती है, और लॉन्च करती है। हेडलेस CLI बाइनरी के लिए, `win-cli` एंट्री पॉइंट का उपयोग करें:

```powershell
irm https://beso1227.github.io/Deltempo/win-cli | Invoke-Expression
```

यदि मैनिफ़ेस्ट पढ़ा नहीं जा सकता, या हैश मेल नहीं खाता, तो इंस्टॉलर निरस्त हो जाता है और डाउनलोड की गई कुछ भी नहीं चलाता — सत्यापन कभी छोड़ा नहीं जाता। ध्यान दें कि `Invoke-Expression` कोई आर्ग्यूमेंट स्वीकार नहीं करता, इसलिए लक्ष्य `-Cli` स्विच के बजाय एंट्री पॉइंट द्वारा चुना जाता है।

### विकल्प 3: `deltempo` कमांड

आपको यह कभी हाथ से नहीं जोड़ना पड़ता। Deltempo पहली बार लॉन्च करने पर एक कंसोल-सबसिस्टम CLI बाइनरी तैयार करता है,
उसे आपके यूज़र `PATH` में जोड़ता है, <kbd>Win</kbd>+<kbd>R</kbd> एलियस रजिस्टर करता है, और आपके PowerShell प्रोफ़ाइल्स में
एक `deltempo` फ़ंक्शन इंस्टॉल करता है — और यदि किसी पुरानी इंस्टॉल का पुराना रजिस्ट्रेशन मिलता है
तो उसे मरम्मत करता है।

जानने योग्य दो बातें:

- **नई टर्मिनल विंडो खोलें।** पहले से खुले शेल अपने साथ शुरू हुए `PATH` को ही बनाए रखते हैं।
- **ऑफ़लाइन पहली रन?** यदि कंसोल बाइनरी तैयार नहीं हो सकती, तो कुछ भी रजिस्टर नहीं होता और कोई
  आधा-इंस्टॉल कमांड पीछे नहीं छोड़ी जाती। डेस्कटॉप ऐप पूरी तरह अप्रभावित रहता है —
  अगली सफल लॉन्च पुनः प्रयास करती है।

इसे स्वयं प्रबंधित करना पसंद है? `deltempo register` माँग पर वही काम करता है,
और `deltempo unregister` इसके द्वारा बनाए गए हर आर्टिफ़ैक्ट को हटा देता है। यह देखने के लिए कि वास्तव में क्या इंस्टॉल है
और कौन-सी बाइनरी इंगित कर रही है, `deltempo register --status` का उपयोग करें।

> संरक्षित सिस्टम स्टेट को छूने वाले कमांड (`restore-points`, `deep-clean` का DISM चरण) मानक, गैर-उन्नत टर्मिनल से
> चलाने पर एक स्पष्ट त्रुटि और गैर-शून्य एग्ज़िट कोड लौटाते हैं।
> उन्हें एडमिनिस्ट्रेटर शेल से चलाएँ, या डेस्कटॉप ऐप का उपयोग करें।

---

<a id="privacy"></a>

## 🔒 गोपनीयता और सुरक्षा गारंटी

Deltempo सुरक्षा और उपयोगकर्ता गोपनीयता को गैर-वार्तानीय मूल सिद्धांतों के रूप में रखकर वास्तुकला बनाया गया है:

1. **शून्य टेलीमेट्री गारंटी**: Deltempo में **शून्य टेलीमेट्री**, एनालिटिक्स लाइब्रेरी, विज्ञापन SDK या बैकग्राउंड पिंग ट्रैकर नहीं हैं। नियमित स्कैनिंग, सफ़ाई, मेमोरी ऑप्टिमाइज़ेशन और अनइंस्टॉलेशन **100% ऑफ़लाइन** चलते हैं।
2. **निर्धारण सुरक्षा पाइपलाइन**:

   ```text
   ┌────────┐     ┌────────┐     ┌─────────┐     ┌────────────┐     ┌─────────┐
   │  SCAN  │ ──► │  PLAN  │ ──► │ PROTECT │ ──► │ REVALIDATE │ ──► │  CLEAN  │
   └────────┘     └────────┘     └─────────┘     └────────────┘     └─────────┘
   ```

   उम्मीदवार फ़ाइलों की योजना बनाई जाती है, संरक्षित डायरेक्टरी सीमाओं (दस्तावेज़, डेस्कटॉप, कोड रिपॉज़िटरी, SSH कुंजियाँ, क्रेडेंशियल्स) के विरुद्ध सत्यापित किया जाता है, और हटाने से ठीक पहले पुनः मान्य किया जाता है।
3. **रीपार्स पॉइंट और ट्रैवर्सल रक्षा**: NTFS डायरेक्टरी जंक्शन, सिंबॉलिक लिंक और वॉल्यूम माउंट पॉइंट्स स्वचालित रूप से अस्वीकार किए जाते हैं, ताकि लक्ष्य सीमाओं के बाहर ट्रैवर्सल हमलों को रोका जा सके।
4. **लोकल ड्राइव सीमा**: विशेष रूप से स्थानीय स्थिर ड्राइवों तक सीमित; रिमोट नेटवर्क शेयर और UNC पाथ अवरुद्ध हैं।
5. **क्रिप्टोग्राफ़िक रिलीज़ सत्यापन**: स्वचालित अपडेट जाँच HTTPS को बाध्य करती हैं और बाइनरी SHA-256 डाइजेस्ट को हस्ताक्षरित GitHub रिलीज़ मैनिफ़ेस्ट के विरुद्ध सत्यापित करती हैं।

---

## 🛠️ सोर्स से बनाना

### आवश्यकताएँ

* Windows 10 या 11 (64-बिट / x64)
* [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
* PowerShell 7+ या Windows PowerShell 5.1

### संकलन और परीक्षण

```powershell
# रिपॉज़िटरी क्लोन करें
git clone https://github.com/Beso1227/Deltempo.git
cd Deltempo

# पूरे समाधान को Release कॉन्फ़िगरेशन में संकलित करें
dotnet build deltempo.sln -c Release

# स्वचालित परीक्षण सूट निष्पादित करें (726 पासिंग यूनिट और इंटीग्रेशन परीक्षण)
dotnet test deltempo.sln -c Release

# स्टैंडअलोन सिंगल-फ़ाइल रिलीज़ बाइनरी (GUI और CLI) पैकेज करें
pwsh -ExecutionPolicy Bypass -File scripts/build_release_exe.ps1
```

परिणामी सिंगल-फ़ाइल एक्ज़ीक्यूटेबल यहाँ प्रकाशित होंगे:

* `publish\Deltempo.exe` (GUI)
* `publish\deltempo_cli.exe` (CLI)

---

## 📜 लाइसेंस

Deltempo **[MIT लाइसेंस](LICENSE)** के तहत लाइसेंस प्राप्त निःशुल्क और ओपन-सोर्स सॉफ़्टवेयर है।

```text
Copyright (c) 2026 Beso1227 / Deltempo Project
```

---

## ❓ अक्सर पूछे जाने वाले प्रश्न

**क्या Deltempo सचमुच निःशुल्क है?**
हाँ। Deltempo पूर्णतः निःशुल्क और MIT ओपन-सोर्स है — कोई सशुल्क टियर नहीं, कोई अपसेल नहीं, कोई खाता नहीं, और कोई विज्ञापन नहीं।

**क्या Deltempo एक अच्छा CCleaner विकल्प है?**
हाँ। Deltempo CCleaner का निःशुल्क, ओपन-सोर्स, शून्य-टेलीमेट्री विकल्प है जिसमें गहन अनइंस्टॉलर, डुप्लिकेट फ़ाइल फ़ाइंडर, बड़ी फ़ाइल हंटर और अंतर्निहित Windows सिस्टम मरम्मत भी शामिल है — बिना किसी इंस्टॉलेशन के।

**क्या Deltempo टेलीमेट्री भेजता है या मेरा पीछा करता है?**
नहीं। Deltempo में शून्य टेलीमेट्री है: कोई एनालिटिक्स लाइब्रेरी नहीं, कोई ट्रैकिंग पिक्सेल नहीं, कोई विज्ञापन SDK नहीं। हर संचालन 100% ऑफ़लाइन चलता है।

**क्या Deltempo को इंस्टॉलर चाहिए?**
नहीं। यह पोर्टेबल सिंगल-फ़ाइल एक्ज़ीक्यूटेबल (~68 MB) है। `Deltempo.exe` सीधे उसी जगह चलाएँ — आपके सिस्टम पर कुछ भी इंस्टॉल नहीं होता।

**क्या ब्राउज़र कैश साफ़ करने से मेरे खातों से लॉगआउट हो जाएगा?**
नहीं। केवल निष्कासन-योग्य कैश डायरेक्टरी हटाई जाती हैं। लॉगिन सत्र, कुकीज़ और पासवर्ड कड़ाई से सुरक्षित रहते हैं।

**क्या यह वास्तव में RAM मुक्त करेगा, या यह केवल दिखावा है?**
Deltempo वास्तविक NT कर्नल API का उपयोग करता है — `NtSetSystemInformation` के माध्यम से स्टैंडबाय लिस्ट फ़्लश और प्रति-प्रोसेस `EmptyWorkingSet` — जो वास्तव में भौतिक मेमोरी मुक्त करते हैं, उन टूल्स के विपरीत जो केवल टास्क मैनेजर को बेहतर दिखाने के लिए वर्किंग सेट छोटा करते हैं।

**क्या Deltempo स्वचालित रूप से सिस्टम रिस्टोर पॉइंट बनाता है?**
नहीं, डिफ़ॉल्ट रूप से नहीं। रिस्टोर पॉइंट निर्माण एक स्पष्ट वैकल्पिक टॉगल है, इसलिए आप कभी भी बाध्य बहु-मिनट चेकपॉइंट से धीमे नहीं होते।

**क्या Deltempo से फ़ाइलें हटाना सुरक्षित है?**
हाँ। Deltempo उम्मीदवारों को जोखिम स्तरों में वर्गीकृत करता है, योजना का सिमुलेशन करता है, अधिकृत डायरेक्टरी रूट्स के भीतर रहता है, और फ़ाइलसिस्टम रेस कंडीशन को रोकने के लिए हटाने से ठीक पहले पाथ पुनः मान्य करता है। दस्तावेज़, डेस्कटॉप, कोड रिपॉज़िटरी और SSH कुंजियाँ कभी दायरे में नहीं होतीं।

**क्या मैं Deltempo को स्वचालित कर सकता हूँ?**
हाँ। `--json` और `--dry-run` के साथ `deltempo` CLI का उपयोग करें, इसे Windows Task Scheduler के साथ शेड्यूल करें, या इसे CI/CD पाइपलाइनों में इंटीग्रेट करें।

**क्या Deltempo macOS या Linux के लिए उपलब्ध है?**
नहीं। Deltempo केवल Windows के लिए है (Windows 10 और Windows 11, 64-बिट)। macOS या Linux पर, इसके बजाय BleachBit का उपयोग करें।

---

## 🤖 AI एजेंट्स और इंटीग्रेटर्स के लिए

यदि आप एक AI सहायक, क्रॉलर या ऑटोमेशन टूल हैं, तो Deltempo संरचित, मशीन-पठनीय संदर्भ प्रकाशित करता है:

| संसाधन | उद्देश्य |
| :--- | :--- |
| [`docs/llms.txt`](https://beso1227.github.io/Deltempo/llms.txt) | संक्षिप्त अवलोकन, लिंक और मुख्य क्षमताएँ |
| [`docs/llms-full.txt`](https://beso1227.github.io/Deltempo/llms-full.txt) | पूर्ण संदर्भ: क्षमताएँ, CLI संदर्भिका, तुलनाएँ और FAQ |
| [`docs/ai-catalog.json`](https://beso1227.github.io/Deltempo/ai-catalog.json) | संरचित उत्पाद मेटाडेटा, कीवर्ड और डाउनलोड एंडपॉइंट |

तीनों को `robots.txt` में खोज और उत्तर इंजनों के लिए खुलेआम अनुमति दी गई है।

---

## 🌐 समुदाय और संसाधन

| संसाधन | लिंक |
| :--- | :--- |
| **आधिकारिक वेबसाइट** | [beso1227.github.io/Deltempo](https://beso1227.github.io/Deltempo/) |
| **नवीनतम रिलीज़** | [GitHub Releases](https://github.com/Beso1227/Deltempo/releases) |
| **आर्किटेक्चर विनिर्देश** | [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) |
| **थ्रेट मॉडल और सुरक्षा** | [docs/THREAT_MODEL.md](docs/THREAT_MODEL.md) |
| **परीक्षण गाइड** | [docs/TESTING.md](docs/TESTING.md) |
| **योगदान गाइड** | [CONTRIBUTING.md](CONTRIBUTING.md) |
| **सुरक्षा नीति** | [SECURITY.md](SECURITY.md) |
| **बग रिपोर्ट और समस्याएँ** | [GitHub Issues](https://github.com/Beso1227/Deltempo/issues) |
