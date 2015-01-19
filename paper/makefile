all: paper

paper: G5-T3-paper.tex G5-T3-paper.bib
	pdflatex G5-T3-paper.tex
	bibtex G5-T3-paper.aux
	pdflatex G5-T3-paper.tex
	pdflatex G5-T3-paper.tex
	
clean: 
	rm -f G5-T3-paper.aux
	rm -f G5-T3-paper.log
	rm -f G5-T3-paper.bbl
	rm -f G5-T3-paper.blg
