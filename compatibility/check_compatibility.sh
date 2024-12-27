target_dir="Rocket.Unturned/"

exit_code=0

for dll in "$target_dir"*.dll 
do
    dll_name=$(basename "$dll" .dll)

    echo -e "\033[36mCompatibility check for \033[35m$dll_name\033[33m"
    echo
    
    suppression_file="$dll_name.suppress.xml"
    [ -f "$suppression_file" ] || suppression_file="default.suppress.xml"

    output=$(
      apicompat \
        --left "$dll" \
        --right "../build/Rocket.Unturned/$dll_name.dll" \
        --noWarn "CP0003" \
        --suppression-file "$suppression_file"
    )

    if [ -n "$output" ]; then
        echo -e "\033[32m$output"
        echo -e "\033[32mCompatibility check successful for \033[35m$dll_name"
    else
        echo -e "\033[31m$output"
        echo -e "\033[31mCompatibility errors detected for \033[35m$dll_name"
        exit_code=1
    fi
    echo -e "\033[0m"
done

exit $exit_code
