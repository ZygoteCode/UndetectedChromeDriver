function DetectChromeDriver()
{
	try
	{
        if (window.document.documentElement.getAttribute("webdriver"))
		{
			return true;
		}
    }
    catch (Exception)
    {
		
    }

    try
	{
       if (navigator.webdriver)
	   {
		   return true;
	   }
    }
    catch (Exception)
    {
		
    }
	
	try
	{
		if (eval.toString().length == 33 && !window.chrome)
		{
			return true;
		}
    }
    catch (Exception)
    {
		
    }

	try
	{
		if (!(typeof InstallTrigger !== 'undefined'))
		{
			if (navigator.plugins.length === 0)
			{
				return true;
			}
		}
    }
    catch (Exception)
    {
		
    }

	try
	{
		let connection = navigator.connection;
		let connectionRtt = connection ? connection.rtt : undefined;

		if (connectionRtt != undefined)
		{
			if (connectionRtt === 0)
			{
				return true;
			}
		}
    }
    catch (Exception)
    {
		
    }

    var documentDetectionKeys = [
        "__webdriver_evaluate",
        "__selenium_evaluate",
        "__webdriver_script_function",
        "__webdriver_script_func",
        "__webdriver_script_fn",
        "__fxdriver_evaluate",
        "__driver_unwrapped",
        "__webdriver_unwrapped",
        "__driver_evaluate",
        "__selenium_unwrapped",
        "__fxdriver_unwrapped",
    ];

    var windowDetectionKeys = [
        "_phantom",
        "__nightmare",
        "_selenium",
        "callPhantom",
        "callSelenium",
        "_Selenium_IDE_Recorder",
    ];

    try
    {
        for (const windowDetectionKey in windowDetectionKeys)
        {
            try
            {
                const windowDetectionKeyValue = windowDetectionKeys[windowDetectionKey];
            
                if (window[windowDetectionKeyValue])
                {
                    return true;
                }
            }
            catch (Exception)
            {

            }
        }
    }
    catch (Exception)
    {

    }

    try
    {
        for (const documentDetectionKey in documentDetectionKeys)
        {
            try
            {
                const documentDetectionKeyValue = documentDetectionKeys[documentDetectionKey];
            
                if (window['document'][documentDetectionKeyValue])
                {
                    return true;
                }
            }
            catch (Exception)
            {

            }
        }
    }
    catch (Exception)
    {

    }

    try
    {
        for (const documentKey in window['document'])
        {
            if (documentKey.match(/\$[a-z]dc_/) && window['document'][documentKey]['cache_'])
            {
                return true;
            }
        }
    }
    catch (Exception)
    {

    }

    try
    {
        if (window['external'] && window['external'].toString() && (window['external'].toString()['indexOf']('Sequentum') != -1))
        {
            return true;
        }
    }
    catch (Exception)
    {

    }

    try
    {
        if (window['document']['documentElement']['getAttribute']('webdriver'))
        {
            return true;
        }
    }
    catch (Exception)
    {

    }

    try
    {
        if (window['document']['documentElement']['getAttribute']('driver'))
        {
            return true;
        }
    }
    catch (Exception)
    {

    }
	
	return false;
}