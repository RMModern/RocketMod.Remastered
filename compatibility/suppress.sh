target_dir="Rocket.Unturned/"

dll_name=$1

suppression_file="$dll_name.suppress.xml"

apicompat --left "$target_dir$dll_name.dll" \
					--right "../build/Rocket.Unturned/$dll_name.dll" \
					--noWarn "CP0003" \
					--generate-suppression-file \
					--suppression-output-file "$suppression_file"
