all: xmlmd.exe musexlat.exe

XMLMD_SOURCES=XmlMarkdown.cs SmartyPants.cs XhtmlWriter.cs

xmlmd.exe: $(XMLMD_SOURCES)
	gmcs -debug -out:$@ -r:System.Web $(XMLMD_SOURCES)

MUSEXLAT_SOURCES=MuseTranslate.cs

musexlat.exe: $(MUSEXLAT_SOURCES)
	gmcs -debug -out:$@ -r:System.Web $(MUSEXLAT_SOURCES)

INSTDIR=$(HOME)/Sites/johnw/App_Code

install: xmlmd.exe
	cvs commit -m changes
	cp $(XMLMD_SOURCES) $(INSTDIR)
	(cd $(INSTDIR); make test) && open ~/Applications/Network/Unison.app

clean:
	rm *.exe *.mdb
