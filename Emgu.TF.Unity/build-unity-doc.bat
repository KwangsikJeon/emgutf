REM go to the folder of the current script
pushd %~p0
cp NamespaceDoc.cs Assets/Emgu.TF/Assets/Scripts
doxygen Emgu.TF.Doxyfile
cd latex
call make
cd ..
cp latex/refman.pdf ../Emgu.TF.Unity/Assets/Emgu.TF/Documentation/Emgu.TF.Documentation.pdf
rm Assets/Emgu.TF/Assets/Scripts/NamespaceDoc.cs
popd